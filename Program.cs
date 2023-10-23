using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace UniversalMaths
{
    internal abstract class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: UniversalMaths <input assembly>");
                return;
            }

            var inputAssemblyPath = args[0];
            var outputAssemblyPath = GenerateOutputPath(inputAssemblyPath);

            var module = ModuleDefMD.Load(inputAssemblyPath);
            var mathMethods = LoadMathMethods();

            ProcessMathCalls(module, mathMethods);

            module.Write(outputAssemblyPath);
            Console.WriteLine($"[+] Saved: {outputAssemblyPath}");
            Console.ReadKey(true);
        }

        private static string GenerateOutputPath(string inputPath)
        {
            var directory = Path.GetDirectoryName(inputPath) ?? throw new InvalidOperationException();
            var fileName = Path.GetFileNameWithoutExtension(inputPath);
            var extension = Path.GetExtension(inputPath);
            return Path.Combine(directory, $"{fileName}_noMaths{extension}");
            
        }

        private static List<MethodInfo> LoadMathMethods()
        {
            return typeof(Math)
                .GetMethods()
                .Where(methodInfo => !new[] { "GetHashCode", "GetType", "ToString", "Equals" }.Contains(methodInfo.Name))
                .ToList();
        }

        private static void ProcessMathCalls(ModuleDef module, List<MethodInfo> mathMethods)
        {
            var methodInstructions = module.GetTypes()
                .Where(t => t.HasMethods)
                .SelectMany(t => t.Methods.Where(m => m.HasBody))
                .SelectMany(method => method.Body.Instructions);

            foreach (var instruction in methodInstructions.Where(instruction => IsMathCall(instruction, mathMethods)))
            {
                HandleMathCall(instruction, mathMethods);
            }
        }


        private static bool IsMathCall(Instruction instruction, IEnumerable<MethodInfo> mathMethods)
        {
            if (instruction.OpCode == OpCodes.Call && instruction.Operand is MemberRef memberRef)
            {
                return mathMethods.Any(method =>
                    method.Name == memberRef.Name &&
                    method.ToString().Replace("System.", string.Empty).Replace(" ", $" {memberRef.Name}") ==
                    memberRef.Signature.ToString());
            }
            return false;
        }

        private static void HandleMathCall(Instruction instruction, List<MethodInfo> mathMethods)
        {
            var ldcValue = (double)instruction.Operand;
            instruction.OpCode = OpCodes.Ldc_R8;
            instruction.Operand = InvokeMathMethod(ldcValue, mathMethods, instruction);
        }

        private static object? InvokeMathMethod(double ldcValue, IEnumerable<MethodInfo> mathMethods, Instruction instruction)
        {
            var resultMethod = mathMethods.FirstOrDefault(method =>
                method.Name == ((MemberRef)instruction.Operand).Name);
            if (resultMethod == null) return null;
            instruction.OpCode = OpCodes.Nop;
            return resultMethod.Invoke(null, new object[] { ldcValue });
        }
    }
}
