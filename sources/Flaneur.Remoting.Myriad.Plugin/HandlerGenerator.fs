module Flaneur.Remoting.HandlerGenerator

open Myriad.Core
open Myriad.Core.Ast
open FSharp.Compiler.Syntax

module Attributes =
  [<System.AttributeUsage(System.AttributeTargets.Interface)>]
  type RemotableAttribute() =
    inherit System.Attribute()


module private List =
  let mapfst (f : 'a -> 'b) (pairs : ('a * 'c) list) : ('b * 'c) list = [
    for (a, c) in pairs -> f a, c
  ]

  let mapsnd (f : 'a -> 'b) (pairs : ('c * 'a) list) : ('c * 'b) list = [
    for (c, a) in pairs -> c, f a
  ]



module private Ast =
  let isInterface =
    function
    | SynTypeDefn (_,
                   SynTypeDefnRepr.ObjectModel (SynTypeDefnKind.Interface, _, _),
                   _,
                   _,
                   _,
                   _) -> true
    | SynTypeDefn (_,
                   SynTypeDefnRepr.ObjectModel (SynTypeDefnKind.Unspecified,
                                                members,
                                                _),
                   _,
                   _,
                   _,
                   _) when
      members
      |> List.forall (function
        | SynMemberDefn.AbstractSlot (_, _, _) -> true
        | _ -> false)
      ->
      true
    | _ -> false

  let isClass =
    function
    | SynTypeDefn (_,
                   SynTypeDefnRepr.ObjectModel (SynTypeDefnKind.Class, _, _),
                   _,
                   _,
                   _,
                   _) -> true
    | SynTypeDefn (_,
                   SynTypeDefnRepr.ObjectModel (SynTypeDefnKind.Unspecified,
                                                members,
                                                _),
                   _,
                   _,
                   _,
                   _) when
      members
      |> List.exists (function
        | SynMemberDefn.ImplicitCtor (_, _, _, _, _, _) -> true
        | _ -> false)
      ->
      true
    | _ -> false


module private Runtime =
  [<Literal>]
  let Namespace = "Flaneur.Remoting"

type private FArg = | FArg of typ : SynType
type private FMethod = | FMethod of ident : Ident * FArg list
type private FInterface = | FInterface of ident : LongIdent * FMethod list

let extensions = [ ".fs" ]
let defaultRange = FSharp.Compiler.Text.range.Zero

module private Extract =

  let rootNsOrModuleIdent (ast : ParsedInput) =
    let (SynModuleOrNamespace (ident, _, _, _, _, _, _, _, _)) =
      match ast with
      | ParsedInput.ImplFile (ParsedImplFileInput (_name,
                                                   _isScript,
                                                   _qualifiedNameOfFile,
                                                   _scopedPragmas,
                                                   _hashDirectives,
                                                   modules,
                                                   _g,
                                                   _)) -> modules |> List.head
      | _ -> invalidOp "Cannot find root namespace or module"

    SynLongIdent.CreateFromLongIdent ident

  let extractLetDecl ast =
    let (SynModuleOrNamespace (_, _, _, declarations, _, _, _, _, _)) =
      match ast with
      | ParsedInput.ImplFile (ParsedImplFileInput (_name,
                                                   _isScript,
                                                   _qualifiedNameOfFile,
                                                   _scopedPragmas,
                                                   _hashDirectives,
                                                   modules,
                                                   _g,
                                                   _)) -> modules |> List.head
      | _ -> invalidOp "Cannot find root namespace or module"

    declarations
    |> List.choose (function
      | SynModuleDecl.Let (a, b, c) -> SynModuleDecl.Let (a, b, c) |> Some
      | _ -> None)

  let rec extractFunArgs args =
    function
    | SynType.Fun (argType, returnType, _, _) ->
      // TODO: can't get param names yet, just use list
      // https://github.com/MoiraeSoftware/myriad/pull/169
      //let arg = FArg.FArg (Ident.Create $"{List.length args}", argType)
      let newArgs = args @ [ FArg.FArg argType ]
      extractFunArgs newArgs returnType
    //| SynType.Fun _ as x ->
    //  invalidOp $"\
    //    Unexpected function shape.\n\
    //    %A{x}"
    //| SynType.LongIdentApp x
    //| SynType.App
    //| SynType.LongIdent
    | _ -> args

  let chooseInterface =
    function
    | SynTypeDefn (SynComponentInfo.SynComponentInfo (_, _, _, ident, _, _, _, _),
                   SynTypeDefnRepr.ObjectModel (SynTypeDefnKind.Interface,
                                                members,
                                                _),
                   _,
                   _,
                   _,
                   _)
    | SynTypeDefn (SynComponentInfo.SynComponentInfo (_, _, _, ident, _, _, _, _),
                   SynTypeDefnRepr.ObjectModel (SynTypeDefnKind.Unspecified,
                                                members,
                                                _),
                   _,
                   _,
                   _,
                   _) when
      members
      |> List.forall (function
        | SynMemberDefn.AbstractSlot (_, _, _) -> true
        | _ -> false)
      ->
      Some (ident, members)
    | _ -> None

  let chooseAbstractMember =
    function
    | SynMemberDefn.AbstractSlot (SynValSig (_,
                                             SynIdent (ident, _),
                                             _,
                                             synType,
                                             _,
                                             _,
                                             _,
                                             _,
                                             _,
                                             _,
                                             _,
                                             _),
                                  _,
                                  _) -> Some (ident, synType)
    | _ -> None


  let serviceSignatures ast =
    Ast.extractTypeDefn ast
    |> List.map (fun (_, typeDefs) ->
      typeDefs
      |> List.filter (fun t ->
        Ast.hasAttribute<Attributes.RemotableAttribute> t))
    |> List.filter (List.isEmpty >> not)
    |> List.concat
    |> List.map (fun typeDef ->
      let (SynTypeDefn (info, typeRepr, _, _, _, _)) = typeDef
      let (SynComponentInfo (_, _, _, componentIdent, _, _, _, _)) = info

      let funcSignature =
        match typeRepr with
        | SynTypeDefnRepr.ObjectModel (_, members, _) ->
          members
          |> List.map (function
            | SynMemberDefn.AbstractSlot (slotSig, _, _) ->
              let (SynValSig (_, ident, _, synType, _, _, _, _, _, _, _, _)) =
                slotSig

              let (SynIdent (ident, _)) = ident
              (ident, (extractFunArgs List.empty synType))
            | _ -> invalidOp "No abstract function found")
        | _ ->
          invalidOp
            "extracServiceSignatures only support Unspecified type interface with abstract method"

      (componentIdent, funcSignature))

let createServiceEndPoint (serviceIdent : Ident) = serviceIdent.idText

let private serializerInterfaceDeclaration =
  let memberFlags : SynMemberFlags = {
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
    SynMemberDefn.AbstractSlot (
      SynValSig.SynValSig (
        SynAttributes.Empty,
        SynIdent.SynIdent (Ident.Create "serialize", None),
        SynValTyparDecls.SynValTyparDecls (None, false),
        SynType.Fun (
          SynType.Var (
            SynTypar.SynTypar (Ident.Create "a", TyparStaticReq.None, false),
            defaultRange
          ),
          SynType.Var (
            SynTypar.SynTypar (Ident.Create "b", TyparStaticReq.None, false),
            defaultRange
          ),
          defaultRange,
          { ArrowRange = defaultRange }
        ),
        SynValInfo.SynValInfo (List.empty, SynArgInfo.Empty),
        true,
        false,
        FSharp.Compiler.Xml.PreXmlDoc.Empty,
        None,
        None,
        defaultRange,
        FSharp.Compiler.SyntaxTrivia.SynValSigTrivia.Zero
      ),
      memberFlags,
      FSharp.Compiler.Text.range ()
    )

  let deserialize =
    SynMemberDefn.AbstractSlot (
      SynValSig.SynValSig (
        SynAttributes.Empty,
        SynIdent.SynIdent (Ident.Create "deserialize", None),
        SynValTyparDecls.SynValTyparDecls (None, false),
        SynType.Fun (
          SynType.Var (
            SynTypar.SynTypar (Ident.Create "b", TyparStaticReq.None, false),
            defaultRange
          ),
          SynType.Var (
            SynTypar.SynTypar (Ident.Create "a", TyparStaticReq.None, false),
            defaultRange
          ),
          defaultRange,
          { ArrowRange = defaultRange }
        ),
        SynValInfo.SynValInfo (List.empty, SynArgInfo.Empty),
        true,
        false,
        FSharp.Compiler.Xml.PreXmlDoc.Empty,
        None,
        None,
        defaultRange,
        FSharp.Compiler.SyntaxTrivia.SynValSigTrivia.Zero
      ),
      memberFlags,
      FSharp.Compiler.Text.range ()
    )

  let typeDef =
    SynTypeDefn.CreateFromRepr (
      Ident.Create "Serializer",
      SynTypeDefnRepr.ObjectModel (
        SynTypeDefnKind.Unspecified,
        [ serialize ; deserialize ],
        defaultRange
      )
    )

  SynModuleDecl.Types ([ typeDef ], defaultRange)

let private hasNoArg (args : SynLongIdent list) =
  if List.length args > 1 then
    false
  else
    let (SynLongIdent.SynLongIdent (ident, _, _)) = List.head args
    let t = ident |> List.head
    t.idText = "unit"

let rec private createApplicativeFunc func argGen count =
  if count < 0 then
    func
  else

    SynExpr.App (
      ExprAtomicFlag.NonAtomic,
      false,
      createApplicativeFunc func argGen (count - 1),
      argGen count,
      defaultRange
    )

let generateHandler serviceIdent (services : (Ident * SynLongIdent list) list) =
  let createNamedSynPat count =
    Array.zeroCreate count
    |> Array.mapi (fun index _ -> SynPat.CreateNamed (Ident.Create $"a{index}"))
    |> Array.toList

  let binding =
    SynBinding (
      None,
      SynBindingKind.Normal,
      false,
      false,
      SynAttributes.Empty,
      FSharp.Compiler.Xml.PreXmlDoc.Empty,
      SynValData.SynValData (None, SynValInfo.Empty, None),
      SynPat.CreateLongIdent (
        SynLongIdent.Create [ "resolve" ],
        [
          SynPat.Paren (
            SynPat.CreateTyped (
              SynPat.Named (
                SynIdent.SynIdent (Ident.Create "serializer", None),
                true,
                None,
                defaultRange
              ),
              SynType.LongIdent (SynLongIdent.Create [ "Serializer" ])
            ),
            defaultRange
          )
          SynPat.Paren (
            SynPat.CreateTyped (
              SynPat.Named (
                SynIdent.SynIdent (Ident.Create "services", None),
                true,
                None,
                defaultRange
              ),
              SynType.LongIdent (SynLongIdent.CreateFromLongIdent serviceIdent)
            ),
            defaultRange
          )
          SynPat.Paren (
            SynPat.CreateTyped (
              SynPat.Named (
                SynIdent.SynIdent (Ident.Create "url", None),
                true,
                None,
                defaultRange
              ),
              SynType.String ()
            ),
            defaultRange
          )
          SynPat.Paren (
            SynPat.CreateTyped (
              SynPat.Named (
                SynIdent.SynIdent (Ident.Create "args", None),
                true,
                None,
                defaultRange
              ),
              SynType.List (SynType.String ())
            ),
            defaultRange
          )
        ]
      ),
      None,
      SynExpr.Match (
        DebugPointAtBinding.Yes defaultRange,
        SynExpr.Tuple (
          false,
          [
            SynExpr.Ident (Ident.Create "url")
            SynExpr.Ident (Ident.Create "args")
          ],
          List.empty,
          defaultRange
        ),
        [
          yield!
            services
            |> List.map (fun (serviceIdent, args) ->
              let serviceEndPoint = createServiceEndPoint serviceIdent
              let serviceHasNoArgs = hasNoArg args
              let argsCount = if serviceHasNoArgs then 0 else List.length args

              SynMatchClause.Create (
                SynPat.Tuple (
                  false,
                  [
                    SynPat.Const (
                      SynConst.CreateString serviceEndPoint,
                      defaultRange
                    )
                    SynPat.ArrayOrList (
                      false,
                      createNamedSynPat argsCount,
                      defaultRange
                    )
                  ],
                  defaultRange
                ),
                None,
                SynExpr.App (
                  ExprAtomicFlag.NonAtomic,
                  false,
                  SynExpr.App (
                    ExprAtomicFlag.NonAtomic,
                    false,
                    (if serviceHasNoArgs then
                       SynExpr.App (
                         ExprAtomicFlag.NonAtomic,
                         false,
                         SynExpr.LongIdent (
                           false,
                           SynLongIdent.Create [ "services" ; serviceEndPoint ],
                           None,
                           defaultRange
                         ),
                         SynExpr.CreateUnit,
                         defaultRange
                       )
                     else
                       (createApplicativeFunc
                         (SynExpr.LongIdent (
                           false,
                           SynLongIdent.Create [ "services" ; serviceEndPoint ],
                           None,
                           defaultRange
                         ))
                         (fun index ->
                           SynExpr.App (
                             ExprAtomicFlag.NonAtomic,
                             false,
                             SynExpr.LongIdent (
                               false,
                               SynLongIdent.Create [
                                 "serializer"
                                 "deserialize"
                               ],
                               None,
                               defaultRange
                             ),
                             SynExpr.Ident (Ident.Create $"a{index}"),
                             defaultRange
                           )
                           |> SynExpr.CreateParen)
                         (argsCount - 1))),
                    SynExpr.Ident (Ident.Create "|>"),
                    defaultRange
                  ),
                  SynExpr.App (
                    ExprAtomicFlag.NonAtomic,
                    false,
                    SynExpr.LongIdent (
                      false,
                      SynLongIdent.Create [ "Observable" ; "map" ],
                      None,
                      defaultRange
                    ),
                    SynExpr.LongIdent (
                      false,
                      SynLongIdent.Create [ "serializer" ; "serialize" ],
                      None,
                      defaultRange
                    ),
                    defaultRange
                  ),
                  defaultRange
                )
              ))
          SynMatchClause.Create (
            SynPat.Tuple (
              false,
              [ SynPat.Wild defaultRange ; SynPat.Wild defaultRange ],
              defaultRange
            ),
            None,
            SynExpr.App (
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
        LetKeyword = Some <| FSharp.Compiler.Text.range ()
        EqualsRange = None
      }
    )

  SynModuleDecl.Let (false, [ binding ], defaultRange)


let generate (ctx : GeneratorContext) =
  let ast, _ =
    Ast.fromFilename ctx.InputFilename |> Async.RunSynchronously |> Array.head

  // TODO set the namespace from config
  // using the existing one won't make sense wehn you gen in a diff project

  let namespaceAndRemotableInterfaces =
    Ast.extractTypeDefn ast
    |> List.filter (fun (_, ts) -> not ts.IsEmpty)
    |> List.mapsnd (
      List.choose Extract.chooseInterface
      >> List.mapsnd (
        List.choose Extract.chooseAbstractMember
        >> List.mapsnd (Extract.extractFunArgs [])
        >> List.map FMethod.FMethod
      )
      >> List.map FInterface.FInterface
    )

  printfn $"%A{namespaceAndRemotableInterfaces}"
  let boop = SynModuleOrNamespace.CreateModule

  // TODO: Recreate nesting of modules
  // If we receive a few types like:
  // - X.Y.Z.MyTypeA
  // - X.Y.MyTypeB
  // - X.Y.MyTypeC
  // we should nest our handler for MyTypeA in a module Z

  //let modu =
  //  SynModuleOrNamespace.CreateModule(
  //  Ident.CreateLong "Flaneur.Remoting.Services",
  //  false,
  //  [
  //    SynModuleDecl.CreateOpen Runtime.Namespace
  //    yield! (
  //      Extract.serviceSignatures ast
  //      |> List.map (fun (serviceIdent, funDef) -> generateHandler serviceIdent funDef))
  //  ])

  Output.Ast []
