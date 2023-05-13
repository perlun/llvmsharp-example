using System;
using System.Runtime.InteropServices;
using Kaleidoscope;
using Kaleidoscope.AST;
using LLVMSharp.Interop;

namespace KaleidoscopeLLVM
{
    internal sealed class CodeGenParserListener : IParserListener
    {
        private readonly CodeGenVisitor visitor;

        private readonly LLVMExecutionEngineRef ee;

        private readonly LLVMPassManagerRef passManager;

        public CodeGenParserListener(LLVMExecutionEngineRef ee, LLVMPassManagerRef passManager, CodeGenVisitor visitor)
        {
            this.visitor = visitor;
            this.ee = ee;
            this.passManager = passManager;
        }

        public void EnterHandleDefinition(FunctionAST data)
        {
        }

        public unsafe void ExitHandleDefinition(FunctionAST data)
        {
            this.visitor.Visit(data);
            LLVMValueRef function = this.visitor.ResultStack.Pop();
            LLVM.DumpValue(function);

            LLVM.RunFunctionPassManager(this.passManager, function);
            LLVM.DumpValue(function); // Dump the function for exposition purposes.
        }

        public void EnterHandleExtern(PrototypeAST data)
        {
        }

        public unsafe void ExitHandleExtern(PrototypeAST data)
        {
            this.visitor.Visit(data);
            LLVM.DumpValue(this.visitor.ResultStack.Pop());
        }

        public void EnterHandleTopLevelExpression(FunctionAST data)
        {
        }

        public unsafe void ExitHandleTopLevelExpression(FunctionAST data)
        {
            this.visitor.Visit(data);
            var anonymousFunction = this.visitor.ResultStack.Pop();
            LLVM.DumpValue(anonymousFunction); // Dump the function for exposition purposes.
            var dFunc = (Program.D)Marshal.GetDelegateForFunctionPointer((IntPtr)LLVM.GetPointerToGlobal(this.ee, anonymousFunction), typeof(Program.D));
            LLVM.RunFunctionPassManager(this.passManager, anonymousFunction);

            LLVM.DumpValue(anonymousFunction); // Dump the function for exposition purposes.
            Console.WriteLine("Evaluated to " + dFunc());
        }
    }
}
