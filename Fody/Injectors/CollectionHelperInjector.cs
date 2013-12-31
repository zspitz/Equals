﻿using Equals.Fody.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Mono.Collections.Generic;

namespace Equals.Fody.Injectors
{
    public static class CollectionHelperInjector
    {
        public static MethodDefinition Inject(ModuleDefinition moduleDefinition)
        {
            var typeAttributes = TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.AutoClass | TypeAttributes.AnsiClass | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit;
            var helperType = new TypeDefinition("Equals", "Helpers", typeAttributes);
            helperType.CustomAttributes.MarkAsGeneratedCode();
            helperType.BaseType = ReferenceFinder.Object.TypeReference;
            moduleDefinition.Types.Add(helperType);

            var methodAttributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Static;
            var method = new MethodDefinition("CollectionEquals", methodAttributes, ReferenceFinder.Boolean.TypeReference);
            helperType.Methods.Add(method);

            var left = method.Parameters.Add("left", ReferenceFinder.IEnumerable.TypeReference);
            var right = method.Parameters.Add("right", ReferenceFinder.IEnumerable.TypeReference);
            
            var body = method.Body;
            var ins = body.Instructions;

            var leftEnumerator = body.Variables.Add("enumerator", ReferenceFinder.IEnumerator.TypeReference);
            var rightEnumerator = body.Variables.Add("rightEnumerator", ReferenceFinder.IEnumerator.TypeReference);
            var leftHasNext = body.Variables.Add("hasNext", ReferenceFinder.Boolean.TypeReference);
            var rightHasNext = body.Variables.Add("rightHasNext", ReferenceFinder.Boolean.TypeReference);

            AddLeftAndRightReferenceEquals(ins, left, right);
            AddLeftAndNullReferenceEquals(ins, left);
            AddRightAndNullReferenceEquals(ins, right);
            
            AddGetEnumerator(ins, left, leftEnumerator);
            AddGetEnumerator(ins, right, rightEnumerator);
              
            AddCollectionLoop(ins, leftEnumerator, leftHasNext, rightEnumerator, rightHasNext);

            body.OptimizeMacros();
            method.CustomAttributes.MarkAsGeneratedCode();

            return method;
        }

        private static void AddCollectionLoop(Collection<Instruction> ins, VariableDefinition leftEnumerator, VariableDefinition leftHasNext,
            VariableDefinition rightEnumerator, VariableDefinition rightHasNext)
        {
            var loopBegin = Instruction.Create(OpCodes.Nop);
            ins.Add(loopBegin);

            AddMoveNext(ins, leftEnumerator, leftHasNext);
            AddMoveNext(ins, rightEnumerator, rightHasNext);

            ins.IfAnd(
                c => AddCheckHasNext(c, leftHasNext, false),
                c => AddCheckHasNext(c, rightHasNext, false),
                t => AddReturnTrue(t),
                e =>
                {
                    e.IfAnd(
                        c => AddCheckHasNext(c, leftHasNext, true),
                        c => AddCheckHasNext(c, rightHasNext, true),
                        t =>
                        {
                            t.If(
                                c => AddCheckCurrent(c, leftEnumerator, rightEnumerator),
                                tt => AddRerurnFalse(tt));
                        },
                        e2 =>
                        {
                            ins.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                            ins.Add(Instruction.Create(OpCodes.Ret));
                        });
                });

            ins.Add(Instruction.Create(OpCodes.Br, loopBegin));
        }

        private static void AddRerurnFalse(Collection<Instruction> tt)
        {
            tt.Add(Instruction.Create(OpCodes.Ldc_I4_0));
            tt.Add(Instruction.Create(OpCodes.Ret));
        }

        private static void AddCheckCurrent(Collection<Instruction> c, VariableDefinition leftEnumerator, VariableDefinition rightEnumerator)
        {
            c.Add(Instruction.Create(OpCodes.Ldloc, leftEnumerator));
            c.Add(Instruction.Create(OpCodes.Callvirt, ReferenceFinder.IEnumerator.GetCurrent));

            c.Add(Instruction.Create(OpCodes.Ldloc, rightEnumerator));
            c.Add(Instruction.Create(OpCodes.Callvirt, ReferenceFinder.IEnumerator.GetCurrent));

            c.Add(Instruction.Create(OpCodes.Call, ReferenceFinder.Object.StaticEquals));

            c.Add(Instruction.Create(OpCodes.Ldc_I4_0));
            c.Add(Instruction.Create(OpCodes.Ceq));
        }

        private static void AddReturnTrue(Collection<Instruction> t)
        {
            t.Add(Instruction.Create(OpCodes.Ldc_I4_1));
            t.Add(Instruction.Create(OpCodes.Ret));
        }

        private static void AddCheckHasNext(Collection<Instruction> ins, VariableDefinition hasNext, bool isTrue)
        {
            ins.Add(Instruction.Create(OpCodes.Ldloc, hasNext));
            ins.Add(Instruction.Create(isTrue ? OpCodes.Ldc_I4_0 : OpCodes.Ldc_I4_1));
            ins.Add(Instruction.Create(OpCodes.Ceq));
        }

        private static void AddMoveNext(Collection<Instruction> ins, VariableDefinition enumerator, VariableDefinition hasNext)
        {
            ins.Add(Instruction.Create(OpCodes.Ldloc, enumerator));
            ins.Add(Instruction.Create(OpCodes.Callvirt, ReferenceFinder.IEnumerator.MoveNext));
            ins.Add(Instruction.Create(OpCodes.Ldc_I4_0));
            ins.Add(Instruction.Create(OpCodes.Ceq));
            ins.Add(Instruction.Create(OpCodes.Stloc, hasNext));
        }

        private static void AddGetEnumerator(Collection<Instruction> ins, ParameterDefinition argument, VariableDefinition enumerator)
        {
            ins.Add(Instruction.Create(OpCodes.Ldarg, argument));
            ins.Add(Instruction.Create(OpCodes.Callvirt, ReferenceFinder.IEnumerable.GetEnumerator));
            ins.Add(Instruction.Create(OpCodes.Stloc, enumerator));
        }

        private static void AddRightAndNullReferenceEquals(Collection<Instruction> ins, ParameterDefinition right)
        {
            ins.If(
                c =>
                {
                    ins.Add(Instruction.Create(OpCodes.Ldarg, right));
                    ins.Add(Instruction.Create(OpCodes.Ldnull));
                    ins.Add(Instruction.Create(OpCodes.Call, ReferenceFinder.Object.ReferenceEquals));
                },
                t =>
                {
                    ins.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                    ins.Add(Instruction.Create(OpCodes.Ret));
                });
        }

        private static void AddLeftAndNullReferenceEquals(Collection<Instruction> ins, ParameterDefinition left)
        {
            ins.If(
                c =>
                {
                    ins.Add(Instruction.Create(OpCodes.Ldarg, left));
                    ins.Add(Instruction.Create(OpCodes.Ldnull));
                    ins.Add(Instruction.Create(OpCodes.Call, ReferenceFinder.Object.ReferenceEquals));
                },
                t =>
                {
                    ins.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                    ins.Add(Instruction.Create(OpCodes.Ret));
                });
        }

        private static void AddLeftAndRightReferenceEquals(Collection<Instruction> ins, ParameterDefinition left, ParameterDefinition right)
        {
            ins.If(
                c =>
                {
                    ins.Add(Instruction.Create(OpCodes.Ldarg, left));
                    ins.Add(Instruction.Create(OpCodes.Ldarg, right));
                    ins.Add(Instruction.Create(OpCodes.Call, ReferenceFinder.Object.ReferenceEquals));
                },
                t =>
                {
                    ins.Add(Instruction.Create(OpCodes.Ldc_I4_1));
                    ins.Add(Instruction.Create(OpCodes.Ret));
                });
        }
    }
}
