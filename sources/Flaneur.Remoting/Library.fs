namespace Flaneur.Remoting

open Myriad.Core
open Myriad.Core.Ast
open FSharp.Compiler.Syntax

module Example =
  let extensions = [ ".fs" ]
  let defaultRange = FSharp.Compiler.Text.range.Zero

  let getServiceModule (ast:ParsedInput) : string = "Services"

  let serializerInterfaceDeclaration = 
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

  let generateResolve serviceType (services: (string * int) list) = 
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
              SynType.LongIdent(SynLongIdent.Create [serviceType])),
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
          |> List.map (fun (serviceEndPoint, argCount) -> 
            SynMatchClause.Create(
              SynPat.Tuple(
                false, 
                [
                  SynPat.Const (SynConst.CreateString serviceEndPoint, defaultRange)
                  // TODO: recursively generate args
                  SynPat.ArrayOrList (false, [], defaultRange)
                ],
                defaultRange
                ),
              None,
              SynExpr.App(
                ExprAtomicFlag.NonAtomic,
                false,
                SynExpr.App (
                  ExprAtomicFlag.NonAtomic,
                  false,
                  // TODO: make this generate function application recursively with the number of args
                  SynExpr.App (
                    ExprAtomicFlag.NonAtomic,
                    false,
                    SynExpr.LongIdent (
                      false, 
                      SynLongIdent.Create ["services"; serviceEndPoint], 
                      None, 
                      defaultRange
                    ),
                    SynExpr.Paren(
                      SynExpr.App(
                        ExprAtomicFlag.NonAtomic,
                        false,
                        SynExpr.LongIdent (false, SynLongIdent.Create ["serializer" ; "deserialize"], None, defaultRange),
                        SynExpr.Ident (Ident.Create "a"),
                        defaultRange
                      ),
                      defaultRange,
                      Some defaultRange,
                      defaultRange
                    ),
                    defaultRange
                  ),
                  SynExpr.CreatePipeRight,
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

    let typeDef = 
      Ast.extractTypeDefn ast
      |> List.map (fun (indentation, typeDefs) -> 
        typeDefs |> List.map (fun typeDef -> 
        let (SynTypeDefn(synComponentInfo, synTypeDefnRepr, _members, _implicitCtor, _, _)) = typeDef
        let (SynComponentInfo(attributes, typeParams, constraints, recordId,_,_,_,_)) = synComponentInfo
        SynTypeDefn.CreateFromRepr(recordId |> List.last, SynTypeDefnRepr.Simple(SynTypeDefnSimpleRepr.TypeAbbrev(ParserDetail.Ok,SynType.CreateLongIdent("string"), FSharp.Compiler.Text.range.Zero),FSharp.Compiler.Text.range.Zero))
        )
      )
      |> List.concat
      

    let moduleDeclarations = [
      getServiceModule ast |> SynModuleDecl.CreateOpen
      serializerInterfaceDeclaration
      generateResolve "RemoteServices" [ ("search", 3) ; ("login", 0) ]
    ]

    //SynModuleOrNamespace.SynModuleOrNamespace([Ident.Create "example"], false, SynModuleOrNamespaceKind.DeclaredNamespace, [d], )
    let modu = 
      SynModuleOrNamespace.CreateModule(
      Ident.CreateLong "Flaneur.Remoting.Services", 
      false, 
      moduleDeclarations)

    Output.Ast [modu]



    