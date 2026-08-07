// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// GlpLoweringVisitor (feature 069, T008-T012) — the lowering bridge (contract lowering-contract.md).
// Maps an ANTLR GlpParser parse tree to the production engine's own AST (out/csharp/lib/compiler/
// ast.cs + lib/analysis/type_checker/type_ast.cs), node-for-node, so both front-ends feed the
// identical shared downstream pipeline (FR-001/FR-002). It constructs ast.cs nodes ONLY; it does not
// touch the partial evaluator, analyzer, compiler, codegen, or any production parser (G3/G4, FR-010).
//
// Fidelity notes (mirroring parser.cs node construction exactly, since IL identity depends on it):
//   - binary operators -> StructTerm(operator-lexeme, [l, r])            (parser.cs:1009,1232)
//   - unary minus      -> StructTerm("neg", [x]); op-as-functor "-(A,B)" -> StructTerm("-", [A,B])
//   - variable reader X? : READER lexeme keeps the '?' in ANTLR; the hand-lexer's Lexeme is the base
//     name, so we strip the trailing '?' (lexer.cs:257) -> VarTerm(name, isReader:true)
//   - integer NUMBER -> boxed long, real -> boxed double (lexer.cs:290-292), InvariantCulture
//   - string literal -> ConstTerm value re-wrapped in double quotes (parser.cs:1102); quoted atom
//     '..' -> bare unescaped string (lexer.cs:343-345)
//   - guard '~G' : production tags the goal functor with a leading '~' then ParseClause strips it into
//     Guard.Negated (parser.cs:517-524); we reproduce that exact two-step.
// Type definitions do NOT reach codegen (Program carries only Procedures); proc-decl arg types affect
// IL only via the top-level type name (constant-type grounding, analyzer.cs:880-891). Type-alt nodes
// are therefore lowered directly to TypeExpr (best-effort), never through the compile path.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using GlpRuntime.Compiler;
using GlpRuntime.Analysis.TypeChecker;

namespace GlpGrammarSpike.Bridge
{
    public sealed class GlpLoweringVisitor : GlpBaseVisitor<object>
    {
        // Entry point (contract G2): parse tree -> root engine AST node.
        public Module Lower(GlpParser.ModuleContext parseTree) => (Module)VisitModule(parseTree);

        // ── module / items ──────────────────────────────────────────────────────
        public override object VisitModule(GlpParser.ModuleContext ctx)
        {
            ModuleDeclaration moduleDecl = null;
            CompileMode mode = CompileMode.User;
            foreach (var d in ctx.directive())
            {
                var r = Visit(d);
                if (r is ModuleDeclaration md) moduleDecl = md;
                else if (r is CompileMode cm) mode = cm;
            }

            var typeDefs = new List<TypeDef>();
            var procDecls = new List<ProcDecl>();
            var procedures = new List<Procedure>();

            List<Clause> run = null;
            string runSig = null;
            int runLine = 0, runCol = 0;
            void Flush()
            {
                if (run != null)
                {
                    procedures.Add(new Procedure(run[0].Head.Functor, run[0].Head.Arity, run, runLine, runCol));
                    run = null; runSig = null;
                }
            }

            foreach (var it in ctx.item())
            {
                var r = Visit(it);
                if (r is Clause cl)
                {
                    var sig = cl.Head.Functor + "/" + cl.Head.Arity;
                    if (run != null && sig == runSig) run.Add(cl);
                    else { Flush(); run = new List<Clause> { cl }; runSig = sig; runLine = cl.Line; runCol = cl.Column; }
                }
                else
                {
                    Flush();
                    if (r is ProcDecl pd) procDecls.Add(pd);
                    else if (r is TypeDef td) typeDefs.Add(td);
                }
            }
            Flush();

            return new Module(moduleDecl, typeDefs, procDecls, null, procedures, mode, 1, 1);
        }

        public override object VisitItem(GlpParser.ItemContext ctx)
        {
            if (ctx.procDecl() != null) return Visit(ctx.procDecl());
            if (ctx.typeDef() != null) return Visit(ctx.typeDef());
            return Visit(ctx.clause());
        }

        // ── directives ──────────────────────────────────────────────────────────
        public override object VisitDirective(GlpParser.DirectiveContext ctx)
        {
            var atoms = ctx.ATOM();
            string kw = atoms[0].GetText();
            if (kw == "module")
                return new ModuleDeclaration((string)Visit(ctx.moduleName()), ctx.Start.Line, ctx.Start.Column);
            if (kw == "stdlib")
                return CompileMode.System;
            if (kw == "mode")
                return atoms.Length > 1 && atoms[1].GetText() == "system" ? CompileMode.System : CompileMode.User;
            return CompileMode.User;
        }

        public override object VisitModuleName(GlpParser.ModuleNameContext ctx)
        {
            var parts = new List<string>();
            foreach (var a in ctx.ATOM()) parts.Add(a.GetText());
            return string.Join(".", parts);
        }

        // ── procedure declarations ───────────────────────────────────────────────
        private sealed class ProcNameInfo { public string Name; public string ModulePath; }

        public override object VisitProcDecl(GlpParser.ProcDeclContext ctx)
        {
            bool exported = false, imported = false;
            var prefix = ctx.ATOM();
            if (prefix != null)
            {
                var t = prefix.GetText();
                if (t == "exported") exported = true;
                else if (t == "imported") imported = true;
            }
            var pn = (ProcNameInfo)Visit(ctx.procName());
            var argTypes = new List<TypeExpr>();
            var argCtxs = ctx.argType();
            if (argCtxs != null)
                foreach (var at in argCtxs) argTypes.Add((TypeExpr)Visit(at));
            return new ProcDecl(pn.Name, argTypes, ctx.Start.Line, ctx.Start.Column,
                exported: exported, imported: imported, modulePath: pn.ModulePath);
        }

        public override object VisitProcName(GlpParser.ProcNameContext ctx)
        {
            var baseName = (string)Visit(ctx.procNameToken());
            var atoms = ctx.ATOM();
            if (atoms == null || atoms.Length == 0)
                return new ProcNameInfo { Name = baseName, ModulePath = null };
            var parts = new List<string> { baseName };
            foreach (var a in atoms) parts.Add(a.GetText());
            string name = parts[parts.Count - 1];
            string modulePath = string.Join("#", parts.GetRange(0, parts.Count - 1));
            return new ProcNameInfo { Name = name, ModulePath = modulePath };
        }

        public override object VisitProcNameToken(GlpParser.ProcNameTokenContext ctx) => ctx.GetText();

        public override object VisitArgType(GlpParser.ArgTypeContext ctx)
        {
            int ln = ctx.Start.Line, co = ctx.Start.Column;
            if (ctx.UNDERSCORE() != null)
                return new PrimitiveModeAlt(ctx.QUESTION() != null, ln, co);

            bool reader = ctx.READER() != null;
            bool isInput = reader || ctx.QUESTION() != null;
            string baseName = reader ? StripQ(ctx.READER().GetText()) : ctx.VARIABLE().GetText();

            var atoms = ctx.ATOM();
            if (atoms != null && atoms.Length > 0)
            {
                var parts = new List<string>();
                foreach (var a in atoms) parts.Add(a.GetText());
                parts.Add(baseName);
                return new TypeRef(string.Join("#", parts), ln, co, isInput, null);
            }

            List<TypeExpr> typeArgs = null;
            var argCtxs = ctx.argType();
            if (argCtxs != null && argCtxs.Length > 0)
            {
                typeArgs = new List<TypeExpr>();
                foreach (var a in argCtxs) typeArgs.Add((TypeExpr)Visit(a));
            }
            return new TypeRef(baseName, ln, co, isInput, typeArgs);
        }

        // ── type definitions (type-checker only; NOT compiled to IL) ─────────────
        public override object VisitTypeDef(GlpParser.TypeDefContext ctx)
        {
            int ln = ctx.Start.Line, co = ctx.Start.Column;
            ITerminalNode nameTok = ctx.READER();
            bool nameIsReader = nameTok != null;
            var vars = ctx.VARIABLE();
            if (!nameIsReader) nameTok = vars[0];
            string name = nameTok.GetText(); // READER text already carries '?'; VARIABLE is plain

            var typeParams = new List<string>();
            for (int i = nameIsReader ? 0 : 1; i < vars.Length; i++) typeParams.Add(vars[i].GetText());

            var alts = new List<TypeExpr>();
            foreach (var ta in ctx.typeAlt()) alts.Add((TypeExpr)Visit(ta));
            return new TypeDef(name, alts, ln, co, typeParams);
        }

        public override object VisitTypeAlt(GlpParser.TypeAltContext ctx) => Visit(ctx.typeExpr());

        public override object VisitTypeExpr(GlpParser.TypeExprContext ctx)
        {
            int ln = ctx.Start.Line, co = ctx.Start.Column;
            if (ctx.typePrimary() != null) return Visit(ctx.typePrimary());
            var l = (TypeExpr)Visit(ctx.typeExpr(0));
            var r = (TypeExpr)Visit(ctx.typeExpr(1));
            string op = ((ITerminalNode)ctx.GetChild(1)).GetText();
            if (op == "\\") return new DiffListAlt(l, r, ln, co);
            return new StructAlt(op, new List<TypeExpr> { l, r }, ln, co);
        }

        public override object VisitTypePrimary(GlpParser.TypePrimaryContext ctx)
        {
            int ln = ctx.Start.Line, co = ctx.Start.Column;
            var first = ctx.GetChild(0);
            if (first is ITerminalNode tn)
            {
                int tt = tn.Symbol.Type;
                if (tt == GlpParser.MINUS && ctx.typePrimary() != null)
                    return new StructAlt("neg", new List<TypeExpr> { (TypeExpr)Visit(ctx.typePrimary()) }, ln, co);
                if (tt == GlpParser.VARIABLE || tt == GlpParser.READER)
                {
                    bool reader = tt == GlpParser.READER;
                    bool isInput = reader || ctx.QUESTION() != null;
                    string nm = reader ? StripQ(tn.GetText()) : tn.GetText();
                    List<TypeExpr> targs = null;
                    var altCtxs = ctx.typeAlt();
                    if (altCtxs != null && altCtxs.Length > 0)
                    {
                        targs = new List<TypeExpr>();
                        foreach (var a in altCtxs) targs.Add((TypeExpr)Visit(a));
                    }
                    return new TypeRef(nm, ln, co, isInput, targs);
                }
                if (tt == GlpParser.UNDERSCORE) return new PrimitiveModeAlt(ctx.QUESTION() != null, ln, co);
                if (tt == GlpParser.NUMBER) return new ConstantAlt(NumberValue(tn), ln, co);
                if (tt == GlpParser.STRING) return new ConstantAlt(StringValue(tn), ln, co);
                if (tt == GlpParser.LPAREN)
                {
                    var alts = ctx.typeAlt();
                    var first0 = (TypeExpr)Visit(alts[0]);
                    if (alts.Length == 1) return first0;
                    var list = new List<TypeExpr>(); foreach (var a in alts) list.Add((TypeExpr)Visit(a));
                    return new StructAlt(",", list, ln, co);
                }
                if (tt == GlpParser.ATOM)
                {
                    var altCtxs = ctx.typeAlt();
                    if (altCtxs != null && altCtxs.Length > 0)
                    {
                        var targs = new List<TypeExpr>(); foreach (var a in altCtxs) targs.Add((TypeExpr)Visit(a));
                        return new StructAlt(AtomText(tn), targs, ln, co);
                    }
                    return new ConstantAlt(AtomText(tn), ln, co);
                }
            }
            if (ctx.typeList() != null) return Visit(ctx.typeList());
            throw new InvalidOperationException("unhandled typePrimary: " + ctx.GetText());
        }

        public override object VisitTypeList(GlpParser.TypeListContext ctx)
        {
            int ln = ctx.Start.Line, co = ctx.Start.Column;
            var alts = ctx.typeAlt();
            if (alts == null || alts.Length == 0) return new ListNilAlt(ln, co);
            bool hasPipe = ctx.PIPE() != null;
            var elems = new List<TypeExpr>();
            TypeExpr tail;
            if (hasPipe)
            {
                for (int i = 0; i < alts.Length - 1; i++) elems.Add((TypeExpr)Visit(alts[i]));
                tail = (TypeExpr)Visit(alts[alts.Length - 1]);
            }
            else
            {
                foreach (var a in alts) elems.Add((TypeExpr)Visit(a));
                tail = new ListNilAlt(ln, co);
            }
            TypeExpr acc = tail;
            for (int i = elems.Count - 1; i >= 0; i--) acc = new ListConsAlt(elems[i], acc, ln, co);
            return acc;
        }

        // ── clause / head / guards / body ────────────────────────────────────────
        public override object VisitClause(GlpParser.ClauseContext ctx)
        {
            var head = (Atom)Visit(ctx.head());
            if (ctx.IMPLIES() == null)
                return new Clause(head, null, null, head.Line, head.Column); // fact

            var gogs = ctx.goalOrGuard();
            if (ctx.PIPE() != null)
            {
                var guards = new List<Guard>();
                foreach (var g in gogs)
                {
                    var goal = (Goal)Visit(g);
                    bool negated = goal.Functor.StartsWith("~", StringComparison.Ordinal);
                    string fn = negated ? goal.Functor.Substring(1) : goal.Functor;
                    guards.Add(new Guard(fn, goal.Args, goal.Line, goal.Column, negated: negated));
                }
                var body = new List<Goal>();
                foreach (var b in ctx.goal()) body.Add((Goal)Visit(b));
                return new Clause(head, guards, body, head.Line, head.Column);
            }
            else
            {
                var body = new List<Goal>();
                foreach (var g in gogs) body.Add((Goal)Visit(g));
                return new Clause(head, null, body, head.Line, head.Column);
            }
        }

        public override object VisitHead(GlpParser.HeadContext ctx)
        {
            int ln = ctx.Start.Line, co = ctx.Start.Column;
            var atomApp = ctx.atomApp();
            if (atomApp == null)
            {
                // (VARIABLE | READER | UNDERSCORE) (ASSIGN|UNIV|UNIV_DECOMPOSE|EQUALS) term
                Term lhs = LeadingLValue(ctx.VARIABLE(), ctx.READER(), ctx.UNDERSCORE());
                string op = HeadLeadOp(ctx);
                var rhs = (Term)Visit(ctx.term());
                return new Atom(op, new List<Term> { lhs, rhs }, ln, co);
            }
            var info = (AtomAppInfo)Visit(atomApp);
            if (ctx.UNIV() != null)
                return new Atom("=..", new List<Term> { AtomAppTerm(info, ln, co), (Term)Visit(ctx.term()) }, ln, co);
            if (ctx.EQUALS() != null)
                return new Atom("=", new List<Term> { AtomAppTerm(info, ln, co), (Term)Visit(ctx.term()) }, ln, co);
            return new Atom(info.Functor, info.Args ?? new List<Term>(), ln, co);
        }

        public override object VisitAtomApp(GlpParser.AtomAppContext ctx)
        {
            var info = new AtomAppInfo { Functor = AtomText(ctx.ATOM()) };
            if (ctx.LPAREN() != null)
            {
                info.Args = new List<Term>();
                foreach (var t in ctx.term()) info.Args.Add((Term)Visit(t));
            }
            return info;
        }

        public override object VisitGoal(GlpParser.GoalContext ctx)
        {
            int ln = ctx.Start.Line, co = ctx.Start.Column;
            var atomApp = ctx.atomApp();
            Goal result;
            if (atomApp == null)
            {
                Term v = MakeVar(ctx.VARIABLE() ?? ctx.READER());
                if (ctx.HASH() != null)
                    result = new RemoteGoal(v, (Goal)Visit(ctx.goal()), ln, co);
                else
                    result = new Goal(VarLeadOp(ctx), new List<Term> { v, (Term)Visit(ctx.term()) }, ln, co);
            }
            else
            {
                var info = (AtomAppInfo)Visit(atomApp);
                if (ctx.HASH() != null)
                    result = new RemoteGoal(new ConstTerm(info.Functor, ln, co), (Goal)Visit(ctx.goal()), ln, co);
                else if (ctx.UNIV() != null)
                    result = new Goal("=..", new List<Term> { AtomAppTerm(info, ln, co), (Term)Visit(ctx.term()) }, ln, co);
                else
                    result = new Goal(info.Functor, info.Args ?? new List<Term>(), ln, co);
            }
            var at = ctx.ATOM();
            if (ctx.AT() != null && at != null)
                return new SpawnGoal(result, AtomText(at), ln, co);
            return result;
        }

        public override object VisitGoalOrGuard(GlpParser.GoalOrGuardContext ctx)
        {
            var inner = (Goal)Visit(ctx.goalOrGuardInner());
            if (ctx.TILDE() != null)
                return new Goal("~" + inner.Functor, inner.Args, inner.Line, inner.Column);
            return inner;
        }

        public override object VisitGoalOrGuardInner(GlpParser.GoalOrGuardInnerContext ctx)
        {
            int ln = ctx.Start.Line, co = ctx.Start.Column;

            if (ctx.LPAREN() != null)
            {
                var gogs = ctx.goalOrGuard();
                if (ctx.SEMICOLON() != null)
                {
                    var a = GoalToTerm((Goal)Visit(gogs[0]));
                    var b = GoalToTerm((Goal)Visit(gogs[1]));
                    return new Goal(";", new List<Term> { a, b }, ln, co);
                }
                return Visit(gogs[0]);
            }

            var atomApp = ctx.atomApp();
            if (atomApp == null)
            {
                var varTok = ctx.VARIABLE() ?? ctx.READER();
                if (varTok != null)
                {
                    Term v = MakeVar(varTok);
                    if (ctx.HASH() != null)
                        return new RemoteGoal(v, (Goal)Visit(ctx.goal()), ln, co);
                    return new Goal(VarLeadOp(ctx), new List<Term> { v, (Term)Visit(ctx.term()) }, ln, co);
                }
                // arith cmpOp arith
                var l = (Term)Visit(ctx.arith(0));
                var r = (Term)Visit(ctx.arith(1));
                string cmp = (string)Visit(ctx.cmpOp());
                return new Goal(cmp, new List<Term> { l, r }, ln, co);
            }

            var info = (AtomAppInfo)Visit(atomApp);
            Goal g;
            if (ctx.HASH() != null)
                g = new RemoteGoal(new ConstTerm(info.Functor, ln, co), (Goal)Visit(ctx.goal()), ln, co);
            else if (ctx.EQUALS() != null)
                g = new Goal("=", new List<Term> { AtomAppTerm(info, ln, co), (Term)Visit(ctx.term()) }, ln, co);
            else
                g = new Goal(info.Functor, info.Args ?? new List<Term>(), ln, co);
            var at = ctx.ATOM();
            if (ctx.AT() != null && at != null)
                return new SpawnGoal(g, AtomText(at), ln, co);
            return g;
        }

        public override object VisitCmpOp(GlpParser.CmpOpContext ctx) => ctx.GetText();

        // ── term / arithmetic expressions ────────────────────────────────────────
        public override object VisitTerm(GlpParser.TermContext ctx)
        {
            if (ctx.primary() != null) return Visit(ctx.primary());
            var l = (Term)Visit(ctx.term(0));
            var r = (Term)Visit(ctx.term(1));
            string op = ((ITerminalNode)ctx.GetChild(1)).GetText();
            return new StructTerm(op, new List<Term> { l, r }, ctx.Start.Line, ctx.Start.Column);
        }

        public override object VisitPrimary(GlpParser.PrimaryContext ctx)
        {
            int ln = ctx.Start.Line, co = ctx.Start.Column;
            var first = ctx.GetChild(0);
            if (first is ITerminalNode tn)
            {
                int tt = tn.Symbol.Type;
                if (IsArithOpTok(tt) && ctx.LPAREN() != null)
                    return new StructTerm(tn.GetText(), TermArgs(ctx.term()), ln, co); // op-as-functor
                if (tt == GlpParser.MINUS && ctx.primary() != null)
                    return new StructTerm("neg", new List<Term> { (Term)Visit(ctx.primary()) }, ln, co);
                if (tt == GlpParser.VARIABLE || tt == GlpParser.READER)
                {
                    Term v = MakeVar(tn);
                    if (ctx.ASSIGN() != null)
                        return new StructTerm(":=", new List<Term> { v, (Term)Visit(ctx.term(0)) }, ln, co);
                    return v;
                }
                if (tt == GlpParser.UNDERSCORE) return new UnderscoreTerm(ln, co, ctx.QUESTION() != null);
                if (tt == GlpParser.NUMBER) return new ConstTerm(NumberValue(tn), ln, co);
                if (tt == GlpParser.STRING) return new ConstTerm(StringValue(tn), ln, co);
                if (tt == GlpParser.LPAREN) return TupleTerm(ctx.term(), ln, co);
                if (tt == GlpParser.ATOM)
                {
                    if (ctx.LPAREN() != null) return new StructTerm(AtomText(tn), TermArgs(ctx.term()), ln, co);
                    return new ConstTerm(AtomText(tn), ln, co);
                }
            }
            if (ctx.list() != null) return Visit(ctx.list());
            throw new InvalidOperationException("unhandled primary: " + ctx.GetText());
        }

        public override object VisitArith(GlpParser.ArithContext ctx)
        {
            if (ctx.arithPrimary() != null) return Visit(ctx.arithPrimary());
            var l = (Term)Visit(ctx.arith(0));
            var r = (Term)Visit(ctx.arith(1));
            string op = ((ITerminalNode)ctx.GetChild(1)).GetText();
            return new StructTerm(op, new List<Term> { l, r }, ctx.Start.Line, ctx.Start.Column);
        }

        public override object VisitArithPrimary(GlpParser.ArithPrimaryContext ctx)
        {
            int ln = ctx.Start.Line, co = ctx.Start.Column;
            var first = ctx.GetChild(0);
            if (first is ITerminalNode tn)
            {
                int tt = tn.Symbol.Type;
                if (IsArithOpTok(tt) && ctx.LPAREN() != null)
                    return new StructTerm(tn.GetText(), TermArgs(ctx.term()), ln, co); // op-as-functor
                if (tt == GlpParser.MINUS && ctx.arithPrimary() != null)
                    return new StructTerm("neg", new List<Term> { (Term)Visit(ctx.arithPrimary()) }, ln, co);
                if (tt == GlpParser.VARIABLE || tt == GlpParser.READER) return MakeVar(tn);
                if (tt == GlpParser.UNDERSCORE) return new UnderscoreTerm(ln, co, ctx.QUESTION() != null);
                if (tt == GlpParser.NUMBER) return new ConstTerm(NumberValue(tn), ln, co);
                if (tt == GlpParser.STRING) return new ConstTerm(StringValue(tn), ln, co);
                if (tt == GlpParser.LPAREN) return TupleTerm(ctx.term(), ln, co);
                if (tt == GlpParser.ATOM)
                {
                    if (ctx.LPAREN() != null) return new StructTerm(AtomText(tn), TermArgs(ctx.term()), ln, co);
                    return new ConstTerm(AtomText(tn), ln, co);
                }
            }
            if (ctx.list() != null) return Visit(ctx.list());
            throw new InvalidOperationException("unhandled arithPrimary: " + ctx.GetText());
        }

        public override object VisitList(GlpParser.ListContext ctx)
        {
            int ln = ctx.Start.Line, co = ctx.Start.Column;
            var terms = ctx.term();
            if (terms == null || terms.Length == 0) return new ListTerm(null, null, ln, co); // []
            bool hasPipe = ctx.PIPE() != null;
            var elems = new List<Term>();
            Term tail;
            if (hasPipe)
            {
                for (int i = 0; i < terms.Length - 1; i++) elems.Add((Term)Visit(terms[i]));
                tail = (Term)Visit(terms[terms.Length - 1]);
            }
            else
            {
                foreach (var t in terms) elems.Add((Term)Visit(t));
                tail = new ListTerm(null, null, ln, co); // nil
            }
            Term acc = tail;
            for (int i = elems.Count - 1; i >= 0; i--) acc = new ListTerm(elems[i], acc, ln, co);
            return acc;
        }

        // ── helpers ──────────────────────────────────────────────────────────────
        private sealed class AtomAppInfo { public string Functor; public List<Term> Args; }

        private static Term AtomAppTerm(AtomAppInfo info, int ln, int co)
            => info.Args != null ? new StructTerm(info.Functor, info.Args, ln, co) : (Term)new ConstTerm(info.Functor, ln, co);

        private List<Term> TermArgs(GlpParser.TermContext[] terms)
        {
            var args = new List<Term>();
            if (terms != null) foreach (var t in terms) args.Add((Term)Visit(t));
            return args;
        }

        private Term TupleTerm(GlpParser.TermContext[] terms, int ln, int co)
        {
            var list = TermArgs(terms);
            if (list.Count == 1) return list[0];
            Term acc = list[list.Count - 1];
            for (int i = list.Count - 2; i >= 0; i--) acc = new StructTerm(",", new List<Term> { list[i], acc }, ln, co);
            return acc;
        }

        private static Term MakeVar(ITerminalNode tn)
        {
            bool reader = tn.Symbol.Type == GlpParser.READER;
            string name = reader ? StripQ(tn.GetText()) : tn.GetText();
            return new VarTerm(name, reader, tn.Symbol.Line, tn.Symbol.Column);
        }

        private static Term LeadingLValue(ITerminalNode v, ITerminalNode r, ITerminalNode u)
        {
            if (u != null) return new UnderscoreTerm(u.Symbol.Line, u.Symbol.Column, false);
            var tn = v ?? r;
            return MakeVar(tn);
        }

        private static string HeadLeadOp(GlpParser.HeadContext ctx)
        {
            if (ctx.ASSIGN() != null) return ":=";
            if (ctx.UNIV() != null) return "=..";
            if (ctx.UNIV_DECOMPOSE() != null) return "..=";
            return "="; // EQUALS
        }

        private static string VarLeadOp(GlpParser.GoalContext ctx)
        {
            if (ctx.ASSIGN() != null) return ":=";
            if (ctx.UNIV() != null) return "=..";
            if (ctx.UNIV_DECOMPOSE() != null) return "..=";
            return "="; // EQUALS
        }

        private static string VarLeadOp(GlpParser.GoalOrGuardInnerContext ctx)
        {
            if (ctx.ASSIGN() != null) return ":=";
            if (ctx.UNIV() != null) return "=..";
            if (ctx.UNIV_DECOMPOSE() != null) return "..=";
            return "="; // EQUALS
        }

        private static StructTerm GoalToTerm(Goal g) => new StructTerm(g.Functor, g.Args, g.Line, g.Column);

        private static bool IsArithOpTok(int tt)
            => tt == GlpParser.PLUS || tt == GlpParser.MINUS || tt == GlpParser.STAR
            || tt == GlpParser.SLASH || tt == GlpParser.SLASH_SLASH || tt == GlpParser.MOD;

        private static string StripQ(string s) => s.Length > 0 && s[s.Length - 1] == '?' ? s.Substring(0, s.Length - 1) : s;

        private static object NumberValue(ITerminalNode tn)
        {
            string text = tn.GetText();
            return text.IndexOf('.') >= 0
                ? (object)double.Parse(text, CultureInfo.InvariantCulture)
                : (object)long.Parse(text, CultureInfo.InvariantCulture);
        }

        // String literal: hand-lexer stores the UNescaped content; parser re-wraps in double quotes.
        private static object StringValue(ITerminalNode tn) => "\"" + UnquoteEscape(tn.GetText()) + "\"";

        // Atom: bare lexeme; a quoted atom '...' stores the UNescaped inner content.
        private static string AtomText(ITerminalNode tn)
        {
            string text = tn.GetText();
            return text.Length > 0 && text[0] == '\'' ? UnquoteEscape(text) : text;
        }

        // Reproduce the hand-lexer's escape handling (lexer.cs:305-331) then drop the surrounding quotes.
        private static string UnquoteEscape(string raw)
        {
            if (raw.Length < 2) return raw;
            var sb = new StringBuilder();
            for (int i = 1; i < raw.Length - 1; i++)
            {
                char c = raw[i];
                if (c == '\\' && i + 1 <= raw.Length - 2)
                {
                    i++;
                    char e = raw[i];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case '\\': sb.Append('\\'); break;
                        case '"': sb.Append('"'); break;
                        case '\'': sb.Append('\''); break;
                        default: sb.Append(e); break;
                    }
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        // ── G1: total rule coverage (contract lowering-contract.md, T012) ────────
        // Every parser rule in Glp.g4 MUST have a corresponding overridden VisitXxx here; a rule
        // without one is a hard error, never a silent pass-through. Asserted by reflection.
        public static void AssertRuleCoverage()
        {
            var t = typeof(GlpLoweringVisitor);
            var missing = new List<string>();
            foreach (var rule in GlpParser.ruleNames)
            {
                string method = "Visit" + char.ToUpperInvariant(rule[0]) + rule.Substring(1);
                var mi = t.GetMethod(method, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
                if (mi == null) missing.Add(rule);
            }
            if (missing.Count > 0)
                throw new InvalidOperationException(
                    "GlpLoweringVisitor is missing a lowering method for grammar rule(s): " + string.Join(", ", missing));
        }
    }
}
