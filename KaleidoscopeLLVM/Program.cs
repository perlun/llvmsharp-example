using System;
using System.Collections.Generic;
using Kaleidoscope;
using LLVMSharp.Interop;

namespace KaleidoscopeLLVM
{
    public static class Program
    {
        public delegate double D();

        private static unsafe int Main(string[] args)
        {
            // Create the module, which holds all the code.
            var context = LLVMContextRef.Create();
            LLVMModuleRef module = context.CreateModuleWithName("my cool jit");

            LLVMBuilderRef builder = LLVM.CreateBuilder();

            LLVM.LinkInMCJIT();
            LLVM.InitializeX86TargetInfo();
            LLVM.InitializeX86Target();
            LLVM.InitializeX86TargetMC();

            if (!module.TryCreateExecutionEngine(out LLVMExecutionEngineRef engine, out string errorMessage))
            {
                Console.WriteLine(errorMessage);
                return 1;
            }

            // Create a function pass manager for this engine
            LLVMPassManagerRef passManager = LLVM.CreateFunctionPassManagerForModule(module);

            // Set up the optimizer pipeline.  Start with registering info about how the
            // target lays out data structures.
            // LLVM.DisposeTargetData(LLVM.GetExecutionEngineTargetData(engine));

            // Provide basic AliasAnalysis support for GVN.
            LLVM.AddBasicAliasAnalysisPass(passManager);

            // Promote allocas to registers.
            LLVM.AddPromoteMemoryToRegisterPass(passManager);

            // Do simple "peephole" optimizations and bit-twiddling optzns.
            LLVM.AddInstructionCombiningPass(passManager);

            // Reassociate expressions.
            LLVM.AddReassociatePass(passManager);

            // Eliminate Common SubExpressions.
            LLVM.AddGVNPass(passManager);

            // Simplify the control flow graph (deleting unreachable blocks, etc).
            LLVM.AddCFGSimplificationPass(passManager);

            LLVM.InitializeFunctionPassManager(passManager);

            var codeGenlistener = new CodeGenParserListener(engine, passManager, new CodeGenVisitor(module, builder));

            // Install standard binary operators.
            // 1 is lowest precedence.
            var binopPrecedence = new Dictionary<char, int>
            {
                ['<'] = 10,
                ['+'] = 20,
                ['-'] = 20,
                ['*'] = 40
            };
            // highest.

            var scanner = new Lexer(Console.In, binopPrecedence);
            var parser = new Parser(scanner, codeGenlistener);

            // Prime the first token.
            Console.Write("ready> ");
            scanner.GetNextToken();

            // Run the main "interpreter loop" now.
            MainLoop(scanner, parser);

            // Print out all of the generated code.
            LLVM.DumpModule(module);

            // Initialize the target registry etc.
            LLVM.InitializeAllTargetInfos();
            LLVM.InitializeAllTargets();
            LLVM.InitializeAllTargetMCs();
            LLVM.InitializeAllAsmParsers();
            LLVM.InitializeAllAsmPrinters();

            // Slightly cumbersome since LLVMSharp expects the input to LLVM.SetTarget() to be a .NET string.
            sbyte* targetTriple = LLVM.GetDefaultTargetTriple();
            //string targetTriple = Marshal.PtrToStringAnsi(targetTriplePtr);
            LLVM.SetTarget(module, targetTriple);

            // TODO: Use LLVMTargetRef.TryGetTargetFromTriple()
            if (!LLVMTargetRef.TryGetTargetFromTriple(SpanExtensions.AsString(targetTriple), out LLVMTargetRef target, out string error))
            {
                Console.Error.WriteLine(error);
                return 1;
            }

            // TODO: Try to convert this to C# syntax w/ LLVMSharp

            // std::string Error;
            // auto Target = TargetRegistry::lookupTarget(TargetTriple, Error);
            //
            // // Print an error and exit if we couldn't find the requested target.
            // // This generally occurs if we've forgotten to initialise the
            // // TargetRegistry or we have a bogus target triple.
            // if (!Target) {
            //     errs() << Error;
            //     return 1;
            // }
            //
            // auto CPU = "generic";
            // auto Features = "";
            //
            // TargetOptions opt;
            // auto RM = Optional<Reloc::Model>();
            // auto TheTargetMachine =
            //     Target->createTargetMachine(TargetTriple, CPU, Features, opt, RM);
            //
            // TheModule->setDataLayout(TheTargetMachine->createDataLayout());
            //
            // auto Filename = "output.o";
            // std::error_code EC;
            // raw_fd_ostream dest(Filename, EC, sys::fs::OF_None);
            //
            // if (EC) {
            //     errs() << "Could not open file: " << EC.message();
            //     return 1;
            // }
            //
            // legacy::PassManager pass;
            // auto FileType = CGFT_ObjectFile;
            //
            // if (TheTargetMachine->addPassesToEmitFile(pass, dest, nullptr, FileType)) {
            //     errs() << "TheTargetMachine can't emit a file of this type";
            //     return 1;
            // }
            //
            // pass.run(*TheModule);
            // dest.flush();
            //
            // outs() << "Wrote " << Filename << "\n";

            LLVM.DisposeModule(module);
            LLVM.DisposePassManager(passManager);

            return 0;
        }

        private static void MainLoop(ILexer lexer, IParser parser)
        {
            // top ::= definition | external | expression | ';'
            while (true)
            {
                Console.Write("ready> ");
                switch (lexer.CurrentToken)
                {
                    case (int)Token.EOF:
                        return;
                    case ';':
                        lexer.GetNextToken();
                        break;
                    case (int)Token.DEF:
                        parser.HandleDefinition();
                        break;
                    case (int)Token.EXTERN:
                        parser.HandleExtern();
                        break;
                    default:
                        parser.HandleTopLevelExpression();
                        break;
                }
            }
        }
    }
}
