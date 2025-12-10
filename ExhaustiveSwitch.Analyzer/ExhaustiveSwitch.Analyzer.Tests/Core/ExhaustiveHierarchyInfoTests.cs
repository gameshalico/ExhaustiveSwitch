using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ExhaustiveSwitch.Analyzer.Tests.Core
{
    public class ExhaustiveHierarchyInfoTests
    {
        /// <summary>
        /// 単純な継承関係の場合、親子関係が正しく構築される
        /// </summary>
        [Fact]
        public void SimpleInheritance_BuildsCorrectHierarchy()
        {
            var code = @"
public class Base { }
public class Derived : Base { }
";
            var compilation = CreateCompilation(code);
            var baseType = compilation.GetTypeByMetadataName("Base")!;
            var derivedType = compilation.GetTypeByMetadataName("Derived")!;

            var allCases = new HashSet<INamedTypeSymbol>(
                new[] { baseType, derivedType },
                SymbolEqualityComparer.Default);

            var hierarchyInfo = new ExhaustiveHierarchyInfo(allCases);

            // AllCasesには両方が含まれる
            Assert.Equal(2, hierarchyInfo.AllCases.Count);
            Assert.Contains(baseType, hierarchyInfo.AllCases);
            Assert.Contains(derivedType, hierarchyInfo.AllCases);

            // Derivedの親はBase
            Assert.True(hierarchyInfo.DirectParentsMap.ContainsKey(derivedType));
            Assert.Single(hierarchyInfo.DirectParentsMap[derivedType]);
            Assert.Contains(baseType, hierarchyInfo.DirectParentsMap[derivedType]);

            // Baseの子はDerived
            Assert.True(hierarchyInfo.DirectChildrenMap.ContainsKey(baseType));
            Assert.Single(hierarchyInfo.DirectChildrenMap[baseType]);
            Assert.Contains(derivedType, hierarchyInfo.DirectChildrenMap[baseType]);

            // Baseには親がいない
            Assert.False(hierarchyInfo.DirectParentsMap.ContainsKey(baseType));
        }

        /// <summary>
        /// インターフェースの実装関係が正しく構築される
        /// </summary>
        [Fact]
        public void InterfaceImplementation_BuildsCorrectHierarchy()
        {
            var code = @"
public interface IBase { }
public class Derived : IBase { }
";
            var compilation = CreateCompilation(code);
            var baseType = compilation.GetTypeByMetadataName("IBase")!;
            var derivedType = compilation.GetTypeByMetadataName("Derived")!;

            var allCases = new HashSet<INamedTypeSymbol>(
                new[] { baseType, derivedType },
                SymbolEqualityComparer.Default);

            var hierarchyInfo = new ExhaustiveHierarchyInfo(allCases);

            // Derivedの親はIBase
            Assert.True(hierarchyInfo.DirectParentsMap.ContainsKey(derivedType));
            Assert.Single(hierarchyInfo.DirectParentsMap[derivedType]);
            Assert.Contains(baseType, hierarchyInfo.DirectParentsMap[derivedType]);

            // IBaseの子はDerived
            Assert.True(hierarchyInfo.DirectChildrenMap.ContainsKey(baseType));
            Assert.Single(hierarchyInfo.DirectChildrenMap[baseType]);
            Assert.Contains(derivedType, hierarchyInfo.DirectChildrenMap[baseType]);
        }

        /// <summary>
        /// 多重継承（インターフェース）の場合、複数の親が記録される
        /// </summary>
        [Fact]
        public void MultipleInterfaces_RecordsAllParents()
        {
            var code = @"
public interface IBase1 { }
public interface IBase2 { }
public class Derived : IBase1, IBase2 { }
";
            var compilation = CreateCompilation(code);
            var base1Type = compilation.GetTypeByMetadataName("IBase1")!;
            var base2Type = compilation.GetTypeByMetadataName("IBase2")!;
            var derivedType = compilation.GetTypeByMetadataName("Derived")!;

            var allCases = new HashSet<INamedTypeSymbol>(
                new[] { base1Type, base2Type, derivedType },
                SymbolEqualityComparer.Default);

            var hierarchyInfo = new ExhaustiveHierarchyInfo(allCases);

            // Derivedの親はIBase1とIBase2の両方
            Assert.True(hierarchyInfo.DirectParentsMap.ContainsKey(derivedType));
            Assert.Equal(2, hierarchyInfo.DirectParentsMap[derivedType].Count);
            Assert.Contains(base1Type, hierarchyInfo.DirectParentsMap[derivedType]);
            Assert.Contains(base2Type, hierarchyInfo.DirectParentsMap[derivedType]);

            // IBase1の子はDerived
            Assert.True(hierarchyInfo.DirectChildrenMap.ContainsKey(base1Type));
            Assert.Contains(derivedType, hierarchyInfo.DirectChildrenMap[base1Type]);

            // IBase2の子はDerived
            Assert.True(hierarchyInfo.DirectChildrenMap.ContainsKey(base2Type));
            Assert.Contains(derivedType, hierarchyInfo.DirectChildrenMap[base2Type]);
        }

        /// <summary>
        /// 中間クラス（AllCasesに含まれない）を跨いで、最も近い親を見つける
        /// </summary>
        [Fact]
        public void IntermediateClass_FindsClosestParentInAllCases()
        {
            var code = @"
public class GrandParent { }
public class Parent : GrandParent { }
public class Child : Parent { }
";
            var compilation = CreateCompilation(code);
            var grandParentType = compilation.GetTypeByMetadataName("GrandParent")!;
            var childType = compilation.GetTypeByMetadataName("Child")!;

            // ParentはAllCasesに含めない
            var allCases = new HashSet<INamedTypeSymbol>(
                new[] { grandParentType, childType },
                SymbolEqualityComparer.Default);

            var hierarchyInfo = new ExhaustiveHierarchyInfo(allCases);

            // Childの親はGrandParent（Parentをスキップ）
            Assert.True(hierarchyInfo.DirectParentsMap.ContainsKey(childType));
            Assert.Single(hierarchyInfo.DirectParentsMap[childType]);
            Assert.Contains(grandParentType, hierarchyInfo.DirectParentsMap[childType]);

            // GrandParentの子はChild
            Assert.True(hierarchyInfo.DirectChildrenMap.ContainsKey(grandParentType));
            Assert.Single(hierarchyInfo.DirectChildrenMap[grandParentType]);
            Assert.Contains(childType, hierarchyInfo.DirectChildrenMap[grandParentType]);
        }

        /// <summary>
        /// ダイヤモンド継承の場合、重複なく親を記録
        /// </summary>
        [Fact]
        public void DiamondInheritance_AvoidsDuplicates()
        {
            var code = @"
public interface IBase { }
public interface ILeft : IBase { }
public interface IRight : IBase { }
public class Derived : ILeft, IRight { }
";
            var compilation = CreateCompilation(code);
            var baseType = compilation.GetTypeByMetadataName("IBase")!;
            var leftType = compilation.GetTypeByMetadataName("ILeft")!;
            var rightType = compilation.GetTypeByMetadataName("IRight")!;
            var derivedType = compilation.GetTypeByMetadataName("Derived")!;

            var allCases = new HashSet<INamedTypeSymbol>(
                new[] { baseType, leftType, rightType, derivedType },
                SymbolEqualityComparer.Default);

            var hierarchyInfo = new ExhaustiveHierarchyInfo(allCases);

            // Derivedの親はILeftとIRightのみ（IBaseは間接的）
            Assert.True(hierarchyInfo.DirectParentsMap.ContainsKey(derivedType));
            Assert.Equal(2, hierarchyInfo.DirectParentsMap[derivedType].Count);
            Assert.Contains(leftType, hierarchyInfo.DirectParentsMap[derivedType]);
            Assert.Contains(rightType, hierarchyInfo.DirectParentsMap[derivedType]);
            Assert.DoesNotContain(baseType, hierarchyInfo.DirectParentsMap[derivedType]);

            // ILeftの親はIBase
            Assert.True(hierarchyInfo.DirectParentsMap.ContainsKey(leftType));
            Assert.Single(hierarchyInfo.DirectParentsMap[leftType]);
            Assert.Contains(baseType, hierarchyInfo.DirectParentsMap[leftType]);

            // IRightの親はIBase
            Assert.True(hierarchyInfo.DirectParentsMap.ContainsKey(rightType));
            Assert.Single(hierarchyInfo.DirectParentsMap[rightType]);
            Assert.Contains(baseType, hierarchyInfo.DirectParentsMap[rightType]);
        }

        /// <summary>
        /// 空のAllCasesの場合、空のマップが構築される
        /// </summary>
        [Fact]
        public void EmptyAllCases_BuildsEmptyMaps()
        {
            var allCases = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            var hierarchyInfo = new ExhaustiveHierarchyInfo(allCases);

            Assert.Empty(hierarchyInfo.AllCases);
            Assert.Empty(hierarchyInfo.DirectChildrenMap);
            Assert.Empty(hierarchyInfo.DirectParentsMap);
        }

        /// <summary>
        /// 単一の型（親がいない）の場合
        /// </summary>
        [Fact]
        public void SingleTypeWithNoParent_BuildsCorrectHierarchy()
        {
            var code = @"
public class Standalone { }
";
            var compilation = CreateCompilation(code);
            var standaloneType = compilation.GetTypeByMetadataName("Standalone")!;

            var allCases = new HashSet<INamedTypeSymbol>(
                new[] { standaloneType },
                SymbolEqualityComparer.Default);

            var hierarchyInfo = new ExhaustiveHierarchyInfo(allCases);

            Assert.Single(hierarchyInfo.AllCases);
            Assert.Contains(standaloneType, hierarchyInfo.AllCases);
            Assert.Empty(hierarchyInfo.DirectChildrenMap);
            Assert.Empty(hierarchyInfo.DirectParentsMap);
        }

        /// <summary>
        /// 複雑な階層構造のテスト
        /// </summary>
        [Fact]
        public void ComplexHierarchy_BuildsCorrectStructure()
        {
            var code = @"
public class Animal { }
public class Mammal : Animal { }
public class Dog : Mammal { }
public class Cat : Mammal { }
public class Bird : Animal { }
";
            var compilation = CreateCompilation(code);
            var animalType = compilation.GetTypeByMetadataName("Animal")!;
            var mammalType = compilation.GetTypeByMetadataName("Mammal")!;
            var dogType = compilation.GetTypeByMetadataName("Dog")!;
            var catType = compilation.GetTypeByMetadataName("Cat")!;
            var birdType = compilation.GetTypeByMetadataName("Bird")!;

            var allCases = new HashSet<INamedTypeSymbol>(
                new[] { animalType, mammalType, dogType, catType, birdType },
                SymbolEqualityComparer.Default);

            var hierarchyInfo = new ExhaustiveHierarchyInfo(allCases);

            // Animalの子はMammalとBird
            Assert.True(hierarchyInfo.DirectChildrenMap.ContainsKey(animalType));
            Assert.Equal(2, hierarchyInfo.DirectChildrenMap[animalType].Count);
            Assert.Contains(mammalType, hierarchyInfo.DirectChildrenMap[animalType]);
            Assert.Contains(birdType, hierarchyInfo.DirectChildrenMap[animalType]);

            // Mammalの子はDogとCat
            Assert.True(hierarchyInfo.DirectChildrenMap.ContainsKey(mammalType));
            Assert.Equal(2, hierarchyInfo.DirectChildrenMap[mammalType].Count);
            Assert.Contains(dogType, hierarchyInfo.DirectChildrenMap[mammalType]);
            Assert.Contains(catType, hierarchyInfo.DirectChildrenMap[mammalType]);

            // Dogの親はMammal
            Assert.True(hierarchyInfo.DirectParentsMap.ContainsKey(dogType));
            Assert.Single(hierarchyInfo.DirectParentsMap[dogType]);
            Assert.Contains(mammalType, hierarchyInfo.DirectParentsMap[dogType]);

            // Mammalの親はAnimal
            Assert.True(hierarchyInfo.DirectParentsMap.ContainsKey(mammalType));
            Assert.Single(hierarchyInfo.DirectParentsMap[mammalType]);
            Assert.Contains(animalType, hierarchyInfo.DirectParentsMap[mammalType]);
        }

        /// <summary>
        /// 中間インターフェースをスキップする複雑なケース
        /// </summary>
        [Fact]
        public void SkipIntermediateInterfaces_FindsCorrectParents()
        {
            var code = @"
public interface IRoot { }
public interface IMiddle : IRoot { }
public interface ILeaf : IMiddle { }
public class Concrete : ILeaf { }
";
            var compilation = CreateCompilation(code);
            var rootType = compilation.GetTypeByMetadataName("IRoot")!;
            var leafType = compilation.GetTypeByMetadataName("ILeaf")!;
            var concreteType = compilation.GetTypeByMetadataName("Concrete")!;

            // IMiddleはAllCasesに含めない
            var allCases = new HashSet<INamedTypeSymbol>(
                new[] { rootType, leafType, concreteType },
                SymbolEqualityComparer.Default);

            var hierarchyInfo = new ExhaustiveHierarchyInfo(allCases);

            // Concreteの親はILeaf（最も近い）
            Assert.True(hierarchyInfo.DirectParentsMap.ContainsKey(concreteType));
            Assert.Single(hierarchyInfo.DirectParentsMap[concreteType]);
            Assert.Contains(leafType, hierarchyInfo.DirectParentsMap[concreteType]);

            // ILeafの親はIRoot（IMiddleをスキップ）
            Assert.True(hierarchyInfo.DirectParentsMap.ContainsKey(leafType));
            Assert.Single(hierarchyInfo.DirectParentsMap[leafType]);
            Assert.Contains(rootType, hierarchyInfo.DirectParentsMap[leafType]);

            // IRootには子が2つ（ILeafとConcrete経由）
            Assert.True(hierarchyInfo.DirectChildrenMap.ContainsKey(rootType));
            Assert.Single(hierarchyInfo.DirectChildrenMap[rootType]);
            Assert.Contains(leafType, hierarchyInfo.DirectChildrenMap[rootType]);
        }

        private static Compilation CreateCompilation(string code)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                new[] { syntaxTree },
                new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            return compilation;
        }
    }
}
