﻿module Compiler
open Ast

let mutable indentationLevel = 0
let mutable isNewLine = true
let indent () = indentationLevel <- indentationLevel + 1
let unindent () = indentationLevel <- indentationLevel - 1
let mutable entryPoint = None

let indentId () =
    indent()
    ""

let unindentId () =
    unindent()
    ""

let output (str : string) : string =
    if isNewLine then
        isNewLine <- false
        (String.replicate indentationLevel "    ") + str
    else
        str

let newline () =
    isNewLine <- true
    "\n"

let rec getDeathExpr (ty : TyExpr) : PosAdorn<Expr> =
    let deathFun = dummyWrap (VarExp {name=dummyWrap "__death"})
    let appliedDeath = TemplateApplyExp { func = deathFun;
                                          templateArgs = dummyWrap {tyExprs = dummyWrap [dummyWrap ty]; 
                                                                    capExprs = dummyWrap []}} |> dummyWrap
    wrapWithType
        ty
        (CallExp {func = appliedDeath;
                  args = dummyWrap []})

and compileType (ty : TyExpr) : string =
    match ty with
        | BaseTy (_, _, bty) ->
            match bty with
                | TyUint8 -> output "uint8_t"
                | TyUint16 -> output "uint16_t"
                | TyUint32 -> output "uint32_t"
                | TyUint64 -> output "uint64_t"
                | TyInt8 -> output "int8_t"
                | TyInt16 -> output "int16_t"
                | TyInt32 -> output "int32_t"
                | TyInt64 -> output "int64_t"
                | TyFloat -> output "float"
                | TyBool -> output "bool"
                | TyUnit -> output "Prelude::Unit"
        | TyModuleQualifier {module_ = (_, _, module_); name=(_, _, name)} ->
            output module_ +
            output "::" +
            output name
        | TyName (_, _, name) ->
            output name
        | TyApply {tyConstructor=(_, _, tyConstructor); args=(_, _, args)} ->
            compileType tyConstructor + compileTemplateApply args
        | ForallTy (_, _, name) ->
            output name
        | ArrayTy {valueType=(_, _, valueType); capacity=(_, _, capacity)} ->
            output "std::array<" + compileType valueType + output "," + compileCap capacity + output ">"
        | FunTy { args=args; returnType=(_, _, returnType) } ->
            output "std::function<" + compileType returnType + output "(" +
            (args |> List.map (unwrap >> compileType) |> String.concat ",")
            + output ")>"
        | RefTy (_, _, ty) ->
            output "std::shared_ptr<" + compileType ty + ">"
        | TupleTy tys ->
            output (sprintf "Prelude::Tuple%d<" (List.length tys)) +
            (tys |> List.map (unwrap >> compileType) |> String.concat ",") +
            output ">"

and compileLeftAssign (left : LeftAssign) : string =
    match left with
        | VarMutation {varName=(_, _, varName)} ->
            output varName
        | ArrayMutation {array=(_, _, array); index=index} ->
            output "(" +
            compileLeftAssign array +
            output ")[" +
            compile index +
            output "]"
        | RecordMutation {record=(_, _, record); fieldName=(_, _, fieldName)} ->
            output "(" +
            compileLeftAssign record +
            output ")." +
            output fieldName

and compilePattern (pattern : PosAdorn<Pattern>) (path : PosAdorn<Expr>) =
    let mutable conditions = []
    let mutable assignments = []
    let rec compilePattern' (pattern : PosAdorn<Pattern>) path : unit =
        match pattern with
            | (_, Some typ, MatchVar {varName=varName}) ->
                let varDec = InternalDeclareVar {varName=varName; typ=dummyWrap typ; right=path}
                assignments <- varDec::assignments
            | (_, _, MatchIntVal intLit) ->
                let check = BinaryOpExp {op=dummyWrap Equal; left=path; right=IntExp intLit |> dummyWrap}
                conditions <- check::conditions
            | (_, _, MatchFloatVal floatLit) ->
                let check = BinaryOpExp {op=dummyWrap Equal; left=path; right=FloatExp floatLit |> dummyWrap}
                conditions <- check::conditions
            | ((_, _, MatchValCon {name=name; innerPattern=innerPattern; id=Some index}) | (_, _, MatchValConModQualifier {modQualifier=(_, _, {name=name}); innerPattern=innerPattern; id=Some index})) ->
                let tag = RecordAccessExp {record=path; fieldName=dummyWrap "tag"}
                let check = BinaryOpExp {op=dummyWrap Equal; left=dummyWrap tag; right=(sprintf "%d" index) |> dummyWrap |> IntExp |> dummyWrap}
                let path' = RecordAccessExp {record=path; fieldName=name} |> dummyWrap
                compilePattern' innerPattern path'
                conditions <- check::conditions
            | ((_, _, MatchEmpty) | (_, _, MatchUnderscore)) -> ()
            | (_, _, MatchRecCon {fields=(_, _, fields)}) ->
                fields |> List.iter (fun (fieldName, fieldPattern) ->
                                        let path' = RecordAccessExp {record=path; fieldName=fieldName} |> dummyWrap
                                        compilePattern' fieldPattern path')
            | (_, _, MatchTuple (_, _, patterns)) ->
                patterns |> List.iteri (fun i pat ->
                                            let path' = RecordAccessExp {record=path; fieldName=dummyWrap ("e" + (sprintf "%d" i))} |> dummyWrap
                                            compilePattern' pat path')
    compilePattern' pattern path
    let truth = dummyWrap (TrueExp (dummyWrap ()))
    let condition = List.fold (fun andString cond ->
                                    BinaryOpExp {op = dummyWrap LogicalAnd;
                                                 left = dummyWrap cond;
                                                 right = andString} |> dummyWrap) truth conditions
    (condition, assignments)
        

and compile ((_, maybeTy, expr) : PosAdorn<Expr>) : string =
    match expr with
        | TrueExp _ ->
            output "true"
        | FalseExp _ ->
            output "false"
        | IntExp (_, _, num) ->
            output num
        | FloatExp (_, _, num) ->
            output num
        | IfElseExp {condition=condition; trueBranch=trueBranch; falseBranch=falseBranch} ->
            output "(" +
            compile condition +
            output " ? " +
            newline() +
            indentId() +
            compile trueBranch +
            unindentId() +
            newline() +
            output ":" +
            newline() +
            indentId() +
            compile falseBranch +
            output ")" +
            unindentId()
        | SequenceExp (_, _, sequence) ->
            let len = List.length sequence
            output "(([&]() -> " +
            compileType (Option.get maybeTy) +
            output " {" +
            newline() +
            indentId() +
            ((List.mapi (fun i seqElement ->
                (if i = len - 1 then
                    output "return "
                else
                    output "") +
                compile seqElement +
                output ";" +
                newline()
            ) sequence) |> String.concat "") +
            unindentId() +
            output "})())"
        | AssignExp {left=(_, _, left); right=right; ref=(_, _, ref)} ->
            output "(" +
            (if ref then
                output "*"
            else
                "") +
            compileLeftAssign left +
            output " = " +
            compile right +
            output ")"
        | CallExp {func=func; args=(_, _, args)} ->
            compile func + output "(" +
            (args |> List.map compile |> String.concat ", ") +
            output ")"
        | UnitExp _ ->
            output "{}"
        | VarExp {name=name} ->
            output (unwrap name)
        | WhileLoopExp {condition=condition; body=body} ->
            output "(([&]() -> " +
            compileType (Option.get maybeTy) +
            output " {" + newline() + indentId() +
            output "while (" + compile condition + ") {" + indentId() + newline() +
            compile body + output ";" + unindentId() + newline() + output "}" + newline() +
            output "return {};" + newline() +
            unindentId() +
            output "})())"
        | CaseExp {on=(posOn, Some onTy, on); clauses=(_, _, clauses)} ->
            let ty = Option.get maybeTy
            let unitTy = BaseTy (dummyWrap TyUnit)
            let onVarName = Guid.string()
            let equivalentExpr =
                List.foldBack
                    (fun (pattern, executeIfMatched) ifElseTree ->
                        let (condition, assignments) = compilePattern pattern (VarExp {name=dummyWrap onVarName} |> wrapWithType onTy)
                        let assignments' = List.map (wrapWithType unitTy) assignments
                        let seq = SequenceExp (dummyWrap (List.append assignments' [executeIfMatched]))
                        IfElseExp {condition=condition; trueBranch=wrapWithType ty seq; falseBranch=ifElseTree} |> wrapWithType ty
                    ) clauses (getDeathExpr ty)
            let decOn = InternalDeclareVar {varName=dummyWrap onVarName; typ=dummyWrap onTy; right=(posOn, Some onTy, on)} |> wrapWithType unitTy
            compile (wrapWithType ty (SequenceExp (wrapWithType ty [decOn; equivalentExpr])))
        | InternalDeclareVar {varName=(_, _, varName); typ=(_, _, typ); right=right} ->
            compileType typ + output " " + output varName + output " = " + compile right
        | TemplateApplyExp {func=func; templateArgs=(_, _, templateArgs)} ->
            compile func + compileTemplateApply templateArgs
        | BinaryOpExp {left=left; op=op; right=right} ->
            let opStr = match unwrap op with
                            | Add -> "+"
                            | BitwiseAnd -> "&"
                            | BitwiseOr -> "|"
                            | Divide -> "/"
                            | Equal -> "=="
                            | Greater -> ">"
                            | GreaterOrEqual -> ">="
                            | Less -> "<"
                            | LessOrEqual -> "<="
                            | LogicalAnd -> "&&"
                            | LogicalOr -> "||"
                            | Modulo -> "%"
                            | Multiply -> "*"
                            | NotEqual -> "!="
                            | Subtract -> "-"
            output "(" + compile left + output " " + output opStr + output " " + compile right + output ")"
        | RecordAccessExp { record=record; fieldName=(_, _, fieldName)} ->
            output "(" + compile record + output ")." + output fieldName
        | LambdaExp {clause=(_, _, {returnTy=(_, _, returnTy); arguments=(_, _, args); body=body})} ->
            output "std::function<" + compileType returnTy + "(" + (args |> List.map (fst >> unwrap >> compileType) |> String.concat ",") + ")>(" +
            output "[=](" + (args |> List.map (fun (ty, name) -> compileType (unwrap ty) + output " " + output (unwrap name)) |> String.concat ", ") +
            output ") -> " + compileType returnTy + output " { " + newline() +
            indentId() + output "return " + compile body + output ";" + unindentId() + newline() + output " })"

and compileTemplate (template : Template) : string = 
    let tyVars = template.tyVars |> unwrap |> List.map (unwrap >> (+) "typename ")
    let capVars = template.capVars |> unwrap |> List.map (unwrap >> (+) "int ")
    output "template<" +
    (List.append tyVars capVars |> String.concat ", " |> output) +
    output ">"

and compileCap (cap : CapacityExpr) : string =
    match cap with
        | CapacityNameExpr (_, _, name) ->
            name
        | CapacityOp { left=(_, _, left); op=(_, _, op); right=(_, _, right) } ->
            "(" + compileCap left + ")" +
            (match op with
                 | CAPPLUS -> "+"
                 | CAPMINUS -> "-"
                 | CAPMULTIPLY -> "*"
                 | CAPDIVIDE -> "/") +
            "(" + compileCap right + ")"
        | CapacityConst (_, _, constant) ->
            constant

and compileTemplateApply (templateApp : TemplateApply) : string =
    output "<" +
    ((List.append
        (templateApp.tyExprs |> unwrap |> List.map (unwrap >> compileType))
        (templateApp.capExprs |> unwrap |> List.map (unwrap >> compileCap))) |> String.concat ", ") +
    output ">"

and compileDec (dec : Declaration) : string =
    match dec with
        | (ExportDec _ | ModuleNameDec _) ->
            ""
        | OpenDec (_, _, openDecs) ->
            openDecs |> (List.map (fun (_, _, modName) ->
                output "using namespace " +
                output modName +
                output ";" +
                newline())) |> String.concat ""
        | FunctionDec {name=(_, _, name); template=maybeTemplate; clause=(_, _, clause)} ->
            (match maybeTemplate with
                | Some (_, _, template) ->
                    compileTemplate template +
                    newline()
                | None ->
                    output "") +
            (clause.returnTy |> unwrap |> compileType) +
            output " " +
            output name +
            output "(" +
            ((clause.arguments |> unwrap |> List.map (fun (ty, name) ->
                                                         (ty |> unwrap |> compileType) +
                                                         output " " +
                                                         (name |> unwrap |> output))) |> String.concat ", ") +
            output ") {" +
            newline() +
            indentId() +
            output "return " +
            compile clause.body +
            output ";" +
            unindentId() +
            newline() +
            output "}"
        | RecordDec {name=(_, _, name); fields=fields; template=maybeTemplate} ->
            (match maybeTemplate with
                | Some (_, _, template) ->
                    compileTemplate template +
                    newline()
                | None ->
                    output "") +
            output "struct " +
            output name +
            output " {" +
            newline() +
            indentId() +
            ((fields |> unwrap |> List.map (fun (fieldTy, fieldName) ->
                                                compileType fieldTy +
                                                output " " +
                                                output fieldName +
                                                output ";" + newline())) |> (String.concat "")) +
            unindentId() +
            output "};"
        | LetDec {varName=(_, _, varName); typ=(_, _, typ); right=right} ->
            compileType typ +
            output " " +
            output varName +
            output " = " +
            compile right
        | UnionDec { name=(_, _, name); valCons=(_, _, valCons); template=maybeTemplate } ->
            (match maybeTemplate with
                | Some (_, _, template) ->
                    compileTemplate template +
                    newline()
                | None ->
                    output "") +
            output "struct " +
            output name +
            output " {" +
            newline() +
            indentId() +
            output "uint8_t tag;" +
            newline() +
            output "union {" +
            newline() +
            indentId() +
            (valCons |> List.map (fun ((_, _, valConName), maybeTy) ->
                (match maybeTy with
                    | None -> output "uint8_t"
                    | Some (_, _, ty) -> compileType ty) +
                output " " + valConName + output ";" + newline()) |> String.concat "") +
            unindentId() +
            output "};" +
            newline() +
            unindentId() +
            output "};" +
            newline() + newline() +
            // Output the function representation of the value constructor
            (valCons |> List.mapi (fun i ((_, _, valConName), maybeTy) ->
                (match maybeTemplate with
                    | Some (_, _, template) ->
                        compileTemplate template +
                        newline() +
                        compileType (TyApply {tyConstructor=dummyWrap (TyName (dummyWrap name));
                                              args=templateToTemplateApply template |> dummyWrap})
                    | None ->
                        compileType (TyName (dummyWrap name))) +
                output " " + output valConName + output "(" +
                (match maybeTy with
                     | None -> ""
                     | Some (_, _, ty) -> compileType ty + output " data") +
                output ") {" + newline() + indentId() +
                output "return {" + (sprintf "%d" i) + output ", " +
                (match maybeTy with
                     | None -> "0"
                     | Some _ -> "data") +
                output "};" + newline() +
                unindentId() + output "}" + newline() + newline()) |> String.concat "")

and compileProgram (modList : Module list) : string =
    output "#include <inttypes.h>" + newline() +
    output "#include <stdlib.h>" + newline() +
    output "#include <array>" + newline() +
    output "#include <stdbool.h>" + newline() + newline() +
    output "template<typename T>" + newline() +
    output "T __death() {" + newline() +
    indentId() + output "exit(1);" + newline() + unindentId() +
    output "}" + newline() + newline() +
    (modList |> List.map (fun (Module module_)->
        let moduleName = Module module_ |> Module.nameInModule |> unwrap
        output "namespace " + moduleName + output " {" + newline() +
        indentId() +
        (module_ |> List.map (fun (_, _, dec) ->
            match dec with
                | FunctionDec {name=(_, _, name)} when name = "main" ->
                    entryPoint <- Some {module_=dummyWrap moduleName; name=dummyWrap name}
                | _ -> ()
            compileDec dec + newline() + newline()) |> String.concat "") +
        unindentId() +
        output "}" + newline() + newline()) |> String.concat "") + newline() + newline() +
        (match entryPoint with
             | None ->
                 printfn "Unable to find program entry point. Please create a function called main."
                 failwith "Error"
             | Some {module_=(_, _, module_); name=(_, _, name)} ->
                 output "int main() {" + newline() + indentId() + output module_ + output "::" + output name +
                 output "();" + newline() + output "return 0;" + unindentId() + newline() + "}")