﻿module TypedAst
open FParsec

// Allows for module type to be used for declaring modules.
type Module = Module of Declaration list

// Tuple of starting position and ending position
// Note: The 'option' keyword indicates that field is optional (can have 'none' as assignment)

// PosAdorn wraps a an AST object in a start and end line position for helpful debugging
// messages. It also includes the type of the object, which starts as 'none'.
// Virtually every object in the AST is a PosAdorn wrapping another object.
and TyAdorn<'a> = (Position * Position) * TyExpr * 'a

// Top level declarations
and FunctionRec = { name     : string;
                    template : Template option;
                    clause   : FunctionClause }

and AliasRec  =   { name     : string
                    template : Template option;
                    typ : TyExpr }

and ValueCon =    string * (TyExpr list)

// Union algrebraic datatype
and UnionRec =    { name     : string;
                    valCons  : ValueCon list;
                    template : Template option }

// Let statement
and LetDecRec = { varName : string;
                  typ     : TyExpr;
                  right   : TyAdorn<Expr> }

// Declaration defined as any of the above.
and Declaration = FunctionDec   of FunctionRec
                | AliasDec      of AliasRec
                | UnionDec      of UnionRec
                | LetDec        of LetDecRec
                | ModuleNameDec of string
                | OpenDec       of string list
                | IncludeDec    of string list
                | InlineCodeDec of string

// A template is associated with a function, record or union
and Template = { tyVars : string list; capVars : string list }

// Use these to apply a template (ex: when calling a function with a template)
and TemplateApply = { tyExprs : TyExpr list; capExprs : CapacityExpr list }

// Capacities are used for making lists and arrays of fixed maximum sizes.
and CapacityArithOp = CapAdd | CapSubtract | CapMultiply | CapDivide
and CapacityUnaryOp = CapNegate
and CapacityArithOpRec = { left : CapacityExpr; op : CapacityArithOp; right : CapacityExpr }
and CapacityUnaryOpRec = { op : CapacityUnaryOp; term : CapacityExpr }
and CapacityExpr = CapacityVar of string
                 | CapacityOp of CapacityArithOpRec
                 | CapacityUnaryOp of CapacityUnaryOpRec
                 | CapacityConst of int64

and BaseTypes = TyUint8
              | TyUint16
              | TyUint32
              | TyUint64
              | TyInt8
              | TyInt16
              | TyInt32
              | TyInt64
              | TyFloat
              | TyDouble
              | TyBool
              | TyUnit
              | TyPointer
              | TyString
              | TyRawPointer
and TyCons = BaseTy of BaseTypes
           | ModuleQualifierTy of ModQualifierRec
           | ArrayTy
           | FunTy
           | RefTy
           | TupleTy
and TyExpr = TyCon of TyCons
                    // For TyCon FunTy, the first element in the TyExpr list is the return type
                    // v this must be a TyCon
           | ConApp of TyExpr * (TyExpr list) * (CapacityExpr list)
           | TyVar of string
                          // The (string list) option indicates the ordering for packed records
           | RecordTy of ((string list) option * Map<string, TyExpr>)
and ConstraintType = IsNum | IsInt | IsReal | IsPacked | HasField of (string * TyExpr) | IsRecord
                      // v Types       v capacities  v constraints on types
and TyScheme = Forall of string list * string list * ((TyExpr * ConstraintType) list) * TyExpr

and DeclarationTy = FunDecTy of TyScheme
                    //              v Types      v capacities
                  | AliasDecTy of string list * string list * TyExpr
                  | LetDecTy of TyExpr
                  | UnionDecTy of string list * string list * ModQualifierRec

// Pattern matching AST datatypes.
and MatchVarRec = { varName : string; mutable_ : bool; typ : TyExpr }
and MatchValConRec = { modQualifier : ModQualifierRec; innerPattern : TyAdorn<Pattern> list; id : int }

and Pattern = MatchVar of MatchVarRec
            | MatchIntVal of int64
            | MatchFloatVal of float
            | MatchValCon of MatchValConRec
            | MatchRecCon of (string * TyAdorn<Pattern>) list
            | MatchUnderscore
            | MatchTuple of TyAdorn<Pattern> list
            | MatchUnit
            | MatchTrue
            | MatchFalse

// Elements of a function clause.
and FunctionClause = {returnTy : TyExpr; arguments : (string * TyExpr) list; body : TyAdorn<Expr>}

// Module qualifier.
and ModQualifierRec = { module_ : string; name : string }

and Direction = Upto
              | Downto

// Other AST objects and their definitions. Most of them are explained within their names.
// Binary operation
and BinaryOpRec =     { left : TyAdorn<Expr>; op : BinaryOps; right : TyAdorn<Expr> }
and IfElseRec =       { condition : TyAdorn<Expr>; trueBranch : TyAdorn<Expr>; falseBranch : TyAdorn<Expr> }
and LetRec =          { left : TyAdorn<Pattern>; right : TyAdorn<Expr> }
// Variable assign
and AssignRec =       { left : TyAdorn<LeftAssign>; right : TyAdorn<Expr>; ref : bool }
and ForLoopRec =      { typ : TyExpr; varName : string; start : TyAdorn<Expr>; direction : Direction; end_ : TyAdorn<Expr>; body : TyAdorn<Expr> }
and WhileLoopRec =    { condition : TyAdorn<Expr>; body : TyAdorn<Expr> }
and DoWhileLoopRec =  { condition : TyAdorn<Expr>; body: TyAdorn<Expr> }
// Pattern matching
and CaseRec =         { on : TyAdorn<Expr>; clauses : (TyAdorn<Pattern> * TyAdorn<Expr>) list }
// Unary operation
and UnaryOpRec =      { op : UnaryOps; exp : TyAdorn<Expr> }
and RecordAccessRec = { record : TyAdorn<Expr>; fieldName : string }
and ArrayAccessRec =  { array : TyAdorn<Expr>; index : TyAdorn<Expr> }
and InternalDeclareVarExpRec = { varName : string; typ : TyExpr; right : TyAdorn<Expr> }
and InternalUsingRec = { varName : string; typ : TyExpr }
and InternalUsingCapRec = { varName : string; cap : CapacityExpr }
and UnsafeTypeCastRec = { exp : TyAdorn<Expr>; typ : TyExpr }
// Function call/apply
and CallRec =         { func : TyAdorn<Expr>; args : TyAdorn<Expr> list }
// Applying the template of a function
and TemplateApplyExpRec = { func : Choice<string, ModQualifierRec>; templateArgs : TemplateApply }
and RecordExprRec =   { packed : bool; initFields : (string * TyAdorn<Expr>) list }
and ArrayMakeExpRec = { typ : TyExpr; initializer : TyAdorn<Expr> option }
and DeclVarExpRec = { varName : string; typ : TyExpr }
and Expr = SequenceExp of TyAdorn<Expr> list
          | BinaryOpExp of BinaryOpRec
          | IfElseExp of IfElseRec
          | LetExp of LetRec
          | DeclVarExp of DeclVarExpRec
          | InternalDeclareVar of InternalDeclareVarExpRec // Only used internally for declaring variables
                                                           // that will actually be outputted by the compiler
          | InternalUsing of InternalUsingRec
          | InternalUsingCap of InternalUsingCapRec
          | InlineCode of string
          | AssignExp of AssignRec
          | ForLoopExp of ForLoopRec
          | WhileLoopExp of WhileLoopRec
          | DoWhileLoopExp of DoWhileLoopRec
          | CaseExp of CaseRec
          | UnaryOpExp of UnaryOpRec
          | RecordAccessExp of RecordAccessRec
          | ArrayAccessExp of ArrayAccessRec
          | VarExp of string * TyExpr list * CapacityExpr list
          | UnitExp
          | TrueExp
          | FalseExp
          | LambdaExp of FunctionClause
          | IntExp of int64
          | Int8Exp of int64
          | UInt8Exp of int64
          | Int16Exp of int64
          | UInt16Exp of int64
          | Int32Exp of int64
          | UInt32Exp of int64
          | Int64Exp of int64
          | UInt64Exp of int64
          | FloatExp of float
          | DoubleExp of float
          | StringExp of string
          | CallExp of CallRec
          | TemplateApplyExp of TemplateApplyExpRec
          | ModQualifierExp of ModQualifierRec * TyExpr list * CapacityExpr list
          | RecordExp of RecordExprRec
          | ArrayLitExp of TyAdorn<Expr> list
          | ArrayMakeExp of ArrayMakeExpRec
          | RefExp of TyAdorn<Expr>
          | TupleExp of TyAdorn<Expr> list
          | QuitExp of TyExpr
          | Smartpointer of TyAdorn<Expr>
          | NullExp
and BinaryOps = Add | Subtract | Multiply | Divide | Modulo | BitwiseOr | BitwiseAnd | BitwiseXor
              | LogicalOr | LogicalAnd | Equal | NotEqual | GreaterOrEqual | LessOrEqual | Greater | Less
              | BitshiftLeft | BitshiftRight
and UnaryOps = LogicalNot | BitwiseNot | Negate | Deref

// Mutations are changes in already declared variables, arrays, records, etc.
and ArrayMutationRec =  { array : LeftAssign; index : TyAdorn<Expr> }
and RecordMutationRec = { record : LeftAssign; fieldName : string }
and LeftAssign = VarMutation of string
               | ModQualifierMutation of ModQualifierRec
               | ArrayMutation of ArrayMutationRec
               | RecordMutation of RecordMutationRec

// Takes in a wrapped AST object, returns the object within the TyAdorn.
let unwrap<'a> ((_, _, c) : TyAdorn<'a>) = c
let getType<'a> ((_, b, _) : TyAdorn<'a>) = b
let getPos<'a> ((a, _, _) : TyAdorn<'a>) = a

let dummyPos : Position = new Position("", -1L, -1L, -1L)

let dummyWrap<'a> c : TyAdorn<'a> = ((dummyPos, dummyPos), TyCon <| BaseTy TyUnit, c)

// Add typing to a TyAdorn.
let wrapWithType<'a> t c : TyAdorn<'a> = ((dummyPos, dummyPos), t, c)

// Turns a capacity expression into a string for debugging (printing error messages)
let rec capacityString cap =
    match cap with
    | CapacityVar name -> name
    | CapacityOp {left=left; op=op; right=right} ->
        let opStr = match op with
                    | CapAdd -> "+"
                    | CapSubtract -> "-"
                    | CapDivide -> "/"
                    | CapMultiply -> "*"
        sprintf "(%s %s %s)" (capacityString left) opStr (capacityString right)
    | CapacityConst number -> sprintf "%i" number
    | CapacityUnaryOp {op=CapNegate; term=term} -> sprintf "-%s" (capacityString term)

let rec typeConString con appliedTo capExprs =
    let typeStrings tys sep = List.map typeString tys |> String.concat sep
    let capacityStrings caps sep = List.map capacityString caps |> String.concat sep
    match con with
    | BaseTy baseTy ->
        match baseTy with
        | TyUint8 -> "uint8"
        | TyUint16 -> "uint16"
        | TyUint32 -> "uint32"
        | TyUint64 -> "uint64"
        | TyInt8 -> "int8"
        | TyInt16 -> "int16"
        | TyInt32 -> "int32"
        | TyInt64 -> "int64"
        | TyBool -> "bool"
        | TyUnit -> "unit"
        | TyFloat -> "float"
        | TyDouble -> "double"
        | TyPointer -> "pointer"
        | TyString -> "string"
        | TyRawPointer -> "rawpointer"
    | ArrayTy ->
        let [arrTy] = appliedTo
        let size =
            match capExprs with
            | [size] ->
                capacityString size
            | _ ->
                "???"
        sprintf "%s[%s]" (typeString arrTy) size
    | FunTy ->
        let retTy::argsTys = appliedTo
        sprintf "(%s) -> %s" (typeStrings argsTys ", ") (typeString retTy)
    | ModuleQualifierTy {module_=module_; name=name} ->
        let genericApplication =
            match appliedTo with
            | [] -> ""
            | _ ->
                if List.length capExprs = 0 then
                    sprintf "<%s>" (typeStrings appliedTo ", ")
                else
                    sprintf "<%s; %s>" (typeStrings appliedTo ", ") (capacityStrings capExprs ", ")
        sprintf "%s:%s%s" module_ name genericApplication
    | RefTy ->
        match appliedTo with
        | [refTy] -> sprintf "%s ref" (typeString refTy)
        | _ -> "ref"
    | TupleTy ->
        typeStrings appliedTo " * "

and typeString (ty : TyExpr) : string =
    match ty with
    | TyCon con ->
        typeConString con [] []
    | ConApp (TyCon con, args, capArgs) ->
        typeConString con args capArgs
    | TyVar name ->
        sprintf "'%s" name
    | RecordTy (Some packed, fields) ->
        sprintf "packed {%s}" (packed |> List.map (fun fieldName -> sprintf "%s : %s" fieldName (Map.find fieldName fields |> typeString)) |> String.concat "; ")
    | RecordTy (None, fields) ->
        sprintf "{%s}" (fields |> Map.toList |> List.map (fun (fieldName, fieldTau) -> sprintf "%s : %s" fieldName (typeString fieldTau)) |> String.concat "; ")
    | _ ->
        failwith "Compiler error in typeString"

and interfaceConstraintString (interfaceConstraint : ConstraintType) =
    match interfaceConstraint with
    | IsNum -> "num"
    | IsInt -> "int"
    | IsReal -> "real"
    | IsPacked -> "packed"
    | HasField (fieldName, fieldTau) -> sprintf "{%s : %s}" fieldName (typeString fieldTau)
    | IsRecord -> "record"

let schemeString (scheme : TyScheme) : string =
    let s1 =
        match scheme with
        | Forall ([], [], _, _) ->
            ""
        | Forall (typeVars, [], _, _) ->
            typeVars |> List.map (fun t -> "'" + t) |> String.concat ", "
        | Forall (typeVars, capVars, _, _) ->
            (typeVars |> List.map (fun t -> "'" + t) |> String.concat ", ") + "; " + (capVars |> String.concat ", ")
    let (Forall (_, _, interfaceConstraints, tau)) = scheme
    let s2 = sprintf "<%s>%s" s1 (typeString tau)
    let s3 =
        match interfaceConstraints with
        | [] -> ""
        | _ -> sprintf " where %s" (interfaceConstraints |> List.map (fun (tau, constr) -> sprintf "%s : %s" (typeString tau) (interfaceConstraintString constr)) |> String.concat ", ")
    s2 + s3

let baseTy b = TyCon <| BaseTy b
let unittype = baseTy TyUnit
let booltype = baseTy TyBool
let int8type = baseTy TyInt8
let uint8type = baseTy TyUint8
let int16type = baseTy TyInt16
let uint16type = baseTy TyUint16
let int32type = baseTy TyInt32
let uint32type = baseTy TyUint32
let int64type = baseTy TyInt64
let uint64type = baseTy TyUint64
let floattype = baseTy TyFloat
let doubletype = baseTy TyDouble
let pointertype = baseTy TyPointer
let stringtype = baseTy TyString
let rawpointertype = baseTy TyRawPointer