namespace Flaneur.Remoting

open Myriad.Core
open Myriad.Core.Ast
open FSharp.Compiler.Syntax

module Example =
  let extensions = [ ".fs" ]
  let defaultRange = FSharp.Compiler.Text.range.Zero


  module Extract = 

    let rootNsOrModuleIdent (ast:ParsedInput) = 
      let (SynModuleOrNamespace(ident, _, _, _, _, _, _, _, _)) = 
        match ast with
        | ParsedInput.ImplFile (ParsedImplFileInput (_name, _isScript, _qualifiedNameOfFile, _scopedPragmas, _hashDirectives, modules, _g, _)) ->
          modules |> List.head 
        | _ -> invalidOp "Cannot find root namespace or module"

      SynLongIdent.CreateFromLongIdent ident

    let rec private extractArgs (synType: SynType) (args: List<SynLongIdent>) = 
      match synType with 
      | SynType.Fun(argType,returnType,_,_) -> 
        match argType with 
        | SynType.LongIdent(ident) -> 
          let newArgs = args @ [ident]
          extractArgs returnType newArgs
        | e -> invalidOp $"Unsupported args types {e}"
      | _ -> args

    let serviceSignatures ast =
      Ast.extractTypeDefn ast 
      |> List.map (fun (_, typeDefs) -> 
        typeDefs |> List.filter (fun t -> Ast.hasAttribute<RequireQualifiedAccessAttribute> t))
      |> List.filter (List.isEmpty >> not)
      |> List.concat 
      |> List.map (fun typeDef -> 
        let (SynTypeDefn(info, typeRepr, _,_,_,_)) = typeDef
        let (SynComponentInfo(_,_,_, componentIdent,_,_,_,_)) = info
        let funcSignature = 
          match typeRepr with 
          | SynTypeDefnRepr.ObjectModel(_, members,_) -> 
            members |> List.map (
              function
              | SynMemberDefn.AbstractSlot (slotSig, _, _) -> 
                let (SynValSig(_, ident, _, synType, _,_,_,_,_,_,_,_)) = slotSig
                let (SynIdent(ident,_)) = ident
                (ident, (extractArgs synType List.empty))
              | _ -> invalidOp "No abstract function found"
            )
          | _ -> invalidOp "extracServiceSignatures only support Unspecified type interface with abstract method"
        (componentIdent, funcSignature)
      )

  let createServiceEndPoint (serviceIdent: Ident) = serviceIdent.idText

  let private serializerInterfaceDeclaration = 
    let memberFlags: SynMemberFlags = {
      IsInstance = false
      IsDispatchSlot = false
      IsOverrideOrExplicitImpl = false
      IsFinal = false
      GetterOrSetterIsCompilerGenerated = false
      MemberKind = SynMemberKind.Member
      Trivia = {
        MemberRange = None 
        OverrideRange = None 
        AbstractRange = Some <| FSharp.Compiler.Text.range ()
        StaticRange = None 
        DefaultRange = None
      }
    }

    let serialize = 
      SynMemberDefn.AbstractSlot(
        SynValSig.SynValSig(
          SynAttributes.Empty,
          SynIdent.SynIdent(Ident.Create "serialize", None),
          SynValTyparDecls.SynValTyparDecls(None, false),
          SynType.Fun(
            SynType.Var(SynTypar.SynTypar(Ident.Create "a", TyparStaticReq.None, false), defaultRange),
            SynType.Var(SynTypar.SynTypar(Ident.Create "b", TyparStaticReq.None, false), defaultRange),
            defaultRange,
            { ArrowRange = defaultRange}
          ),
          SynValInfo.SynValInfo(List.empty, SynArgInfo.Empty),
          true,
          false,
          FSharp.Compiler.Xml.PreXmlDoc.Empty,
          None,
          None,
          defaultRange,
          FSharp.Compiler.SyntaxTrivia.SynValSigTrivia.Zero
        ),
        memberFlags,
        FSharp.Compiler.Text.range()
      )

    let deserialize = 
      SynMemberDefn.AbstractSlot(
        SynValSig.SynValSig(
          SynAttributes.Empty,
          SynIdent.SynIdent(Ident.Create "deserialize", None),
          SynValTyparDecls.SynValTyparDecls(None, false),
          SynType.Fun(
            SynType.Var(SynTypar.SynTypar(Ident.Create "b", TyparStaticReq.None, false), defaultRange),
            SynType.Var(SynTypar.SynTypar(Ident.Create "a", TyparStaticReq.None, false), defaultRange),
            defaultRange,
            { ArrowRange = defaultRange}
          ),
          SynValInfo.SynValInfo(List.empty, SynArgInfo.Empty),
          true,
          false,
          FSharp.Compiler.Xml.PreXmlDoc.Empty,
          None,
          None,
          defaultRange,
          FSharp.Compiler.SyntaxTrivia.SynValSigTrivia.Zero
        ),
        memberFlags,
        FSharp.Compiler.Text.range()
      )

    let typeDef = SynTypeDefn.CreateFromRepr (
      Ident.Create "Serializer", 
      SynTypeDefnRepr.ObjectModel(
        SynTypeDefnKind.Unspecified, 
        [ serialize; deserialize ], 
        defaultRange)
    )
    SynModuleDecl.Types([typeDef], defaultRange)

  let private hasNoArg (args: SynLongIdent list) =
    if List.length args > 1 then false else
    let (SynLongIdent.SynLongIdent(ident, _,_)) = List.head args
    let t = ident |> List.head 
    t.idText = "unit"

  let rec private createApplicativeFunc func argGen count = 
    if count < 0 then func else

    SynExpr.App (
      ExprAtomicFlag.NonAtomic,
      false,
      createApplicativeFunc func argGen (count - 1),
      argGen count,
      defaultRange
    )
    
  let generateResolve serviceIdent (services: (Ident * SynLongIdent list) list) = 
    let createNamedSynPat count = 
      Array.zeroCreate count 
      |> Array.mapi (fun index _ -> SynPat.CreateNamed (Ident.Create $"a{index}"))
      |> Array.toList

    let binding = SynBinding(
      None, 
      SynBindingKind.Normal, 
      false, 
      false, 
      SynAttributes.Empty,
      FSharp.Compiler.Xml.PreXmlDoc.Empty,
      SynValData.SynValData(None, SynValInfo.Empty, None),
      SynPat.CreateLongIdent(
        SynLongIdent.Create ["resolve"],
        [
          SynPat.Paren(
            SynPat.CreateTyped(
              SynPat.Named(SynIdent.SynIdent(Ident.Create "serializer", None), true, None, defaultRange),
              SynType.LongIdent(SynLongIdent.Create ["Serializer"])),
            defaultRange
          )
          SynPat.Paren(
            SynPat.CreateTyped(
              SynPat.Named(SynIdent.SynIdent(Ident.Create "services", None), true, None, defaultRange),
              SynType.LongIdent(SynLongIdent.CreateFromLongIdent serviceIdent)),
            defaultRange
          )
          SynPat.Paren(
            SynPat.CreateTyped(
              SynPat.Named(SynIdent.SynIdent(Ident.Create "url", None), true, None, defaultRange),
              SynType.String()),
            defaultRange
          )
          SynPat.Paren(
            SynPat.CreateTyped(
              SynPat.Named(SynIdent.SynIdent(Ident.Create "args", None), true, None, defaultRange),
              SynType.List(SynType.String())),
            defaultRange
          )
        ]
      ),
      None,
      SynExpr.Match(
        DebugPointAtBinding.Yes defaultRange,
        SynExpr.Tuple(
          false, 
          [
            SynExpr.Ident (Ident.Create "url")
            SynExpr.Ident (Ident.Create "args")
          ], 
          List.empty, 
          defaultRange),
        [
          yield! services 
          |> List.map (fun (serviceIdent, args) -> 
            let serviceEndPoint = createServiceEndPoint serviceIdent
            let serviceHasNoArgs = hasNoArg args
            let argsCount = if serviceHasNoArgs then 0 else List.length args
            SynMatchClause.Create(
              SynPat.Tuple(
                false, 
                [
                  SynPat.Const (SynConst.CreateString serviceEndPoint, defaultRange)
                  SynPat.ArrayOrList (false, createNamedSynPat argsCount, defaultRange)
                ],
                defaultRange
                ),
              None,
              SynExpr.App(
                ExprAtomicFlag.NonAtomic,
                false,
                SynExpr.App(
                  ExprAtomicFlag.NonAtomic,
                  false,
                  (if serviceHasNoArgs then 
                    SynExpr.App (
                      ExprAtomicFlag.NonAtomic, 
                      false, 
                      SynExpr.LongIdent (false, SynLongIdent.Create ["services"; serviceEndPoint], None, defaultRange),
                      SynExpr.CreateUnit,
                      defaultRange)
                  else
                    (createApplicativeFunc
                      (SynExpr.LongIdent (false, SynLongIdent.Create ["services"; serviceEndPoint], None, defaultRange))
                      (fun index -> 
                          SynExpr.App (
                            ExprAtomicFlag.NonAtomic,
                            false,
                            SynExpr.LongIdent (false, SynLongIdent.Create ["serializer"; "deserialize"], None, defaultRange),
                            SynExpr.Ident (Ident.Create $"a{index}"),
                            defaultRange
                          )
                          |> SynExpr.CreateParen)
                       (argsCount - 1)
                    )),
                  SynExpr.Ident (Ident.Create "|>"),
                  defaultRange
                ),
                SynExpr.App (
                  ExprAtomicFlag.NonAtomic,
                  false,
                  SynExpr.LongIdent(false, SynLongIdent.Create ["Observable";"map"],None, defaultRange),
                  SynExpr.LongIdent(false, SynLongIdent.Create ["serializer";"serialize"], None, defaultRange),
                  defaultRange
                ),
                defaultRange
              )
            )          
          )
          SynMatchClause.Create(
            SynPat.Tuple(
              false, 
              [
                SynPat.Wild defaultRange
                SynPat.Wild defaultRange
              ],
              defaultRange
              ),
            None,
            SynExpr.App(
              ExprAtomicFlag.NonAtomic, 
              false,
              SynExpr.Ident (Ident.Create "invalidOp"),
              SynExpr.Const (SynConst.CreateString "missing case", defaultRange),
              defaultRange
            )
          )
        ],
        defaultRange,
        FSharp.Compiler.SyntaxTrivia.SynExprMatchTrivia.Zero
      ),
      defaultRange,
      DebugPointAtBinding.NoneAtLet,
      {
        LetKeyword = Some <| FSharp.Compiler.Text.range()
        EqualsRange = None
      }
      )
      
    SynModuleDecl.Let (false, [binding], defaultRange)


  let generate (ctx : GeneratorContext) =
    printfn $"{Myriad.Core.Generation.header}"
    let ast, _ =
      Ast.fromFilename ctx.InputFilename
      |> Async.RunSynchronously
      |> Array.head

    let modu = 
      SynModuleOrNamespace.CreateModule(
      Ident.CreateLong "Flaneur.Remoting.Services", 
      false, 
      [
        SynOpenDeclTarget.ModuleOrNamespace (Extract.rootNsOrModuleIdent ast, defaultRange) |> SynModuleDecl.CreateOpen
        serializerInterfaceDeclaration
        yield! (
          Extract.serviceSignatures ast 
          |> List.map (fun (serviceIdent, funDef) -> generateResolve serviceIdent funDef))
      ])

    Output.Ast [modu]



    