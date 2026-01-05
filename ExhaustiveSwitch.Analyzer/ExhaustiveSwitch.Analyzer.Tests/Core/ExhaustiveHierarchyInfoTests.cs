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
        /// For simple inheritance, parent-child relationship is correctly built
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

            // Both are included in AllCases
            Assert.Equal(2, hierarchyInfo.AllCases.Count);
            Assert.Contains(baseType, hierarchyInfo.AllCases);
            Assert.Contains(derivedType, hierarchyInfo.AllCases);

            // Derived's parent is Base
            Assert.True(hierarchyInfo.DirectParentsMap.ContainsKey(derivedType));
            Assert.Single(hierarchyInfo.DirectParentsMap[derivedType]);
            Assert.Contains(baseType, hierarchyInfo.DirectParentsMap[derivedType]);

            // Base's child is Derived
            Assert.True(hierarchyInfo.DirectChildrenMap.ContainsKey(baseType));
            Assert.Single(hierarchyInfo.DirectChildrenMap[baseType]);
            Assert.Contains(derivedType, hierarchyInfo.DirectChildrenMap[baseType]);

            // Base has no parent
            Assert.False(hierarchyInfo.DirectParentsMap.ContainsKey(baseType));
        }

        /// <summary>
        /// Interface implementation relationship is correctly built
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

            // Derived's parent is IBase
            Assert.True(hierarchyInfo.DirectParentsMap.ContainsKey(derivedType));
            Assert.Single(hierarchyInfo.DirectParentsMap[derivedType]);
            Assert.Contains(baseType, hierarchyInfo.DirectParentsMap[derivedType]);

            // IBase's child is Derived
            Assert.True(hierarchyInfo.DirectChildrenMap.ContainsKey(baseType));
            Assert.Single(hierarchyInfo.DirectChildrenMap[baseType]);
            Assert.Contains(derivedType, hierarchyInfo.DirectChildrenMap[baseType]);
        }

        /// <summary>
        /// For multiple inheritance (interfaces), multiple parents are recorded
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

            // Derived's parents are both IBase1 and IBase2
            Assert.True(hierarchyInfo.DirectParentsMap.ContainsKey(derivedType));
            Assert.Equal(2, hierarchyInfo.DirectParentsMap[derivedType].Count);
            Assert.Contains(base1Type, hierarchyInfo.DirectParentsMap[derivedType]);
            Assert.Contains(base2Type, hierarchyInfo.DirectParentsMap[derivedType]);

            // IBase1's child is Derived
            Assert.True(hierarchyInfo.DirectChildrenMap.ContainsKey(base1Type));
            Assert.Contains(derivedType, hierarchyInfo.DirectChildrenMap[base1Type]);

            // IBase2's child is Derived
            Assert.True(hierarchyInfo.DirectChildrenMap.ContainsKey(base2Type));
            Assert.Contains(derivedType, hierarchyInfo.DirectChildrenMap[base2Type]);
        }

        /// <summary>
        /// Find closest parent across intermediate class (not included in AllCases)
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

            // Don't include Parent in AllCases
            var allCases = new HashSet<INamedTypeSymbol>(
                new[] { grandParentType, childType },
                SymbolEqualityComparer.Default);

            var hierarchyInfo = new ExhaustiveHierarchyInfo(allCases);

            // Child's parent is GrandParent (skipping Parent)
            Assert.True(hierarchyInfo.DirectParentsMap.ContainsKey(childType));
            Assert.Single(hierarchyInfo.DirectParentsMap[childType]);
            Assert.Contains(grandParentType, hierarchyInfo.DirectParentsMap[childType]);

            // GrandParent's child is Child
            Assert.True(hierarchyInfo.DirectChildrenMap.ContainsKey(grandParentType));
            Assert.Single(hierarchyInfo.DirectChildrenMap[grandParentType]);
            Assert.Contains(childType, hierarchyInfo.DirectChildrenMap[grandParentType]);
        }

        /// <summary>
        /// For diamond inheritance, record parents without duplication
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

            // Derived's parents are only ILeft and IRight (IBase is indirect)
            Assert.True(hierarchyInfo.DirectParentsMap.ContainsKey(derivedType));
            Assert.Equal(2, hierarchyInfo.DirectParentsMap[derivedType].Count);
            Assert.Contains(leftType, hierarchyInfo.DirectParentsMap[derivedType]);
            Assert.Contains(rightType, hierarchyInfo.DirectParentsMap[derivedType]);
            Assert.DoesNotContain(baseType, hierarchyInfo.DirectParentsMap[derivedType]);

            // ILeft's parent is IBase
            Assert.True(hierarchyInfo.DirectParentsMap.ContainsKey(leftType));
            Assert.Single(hierarchyInfo.DirectParentsMap[leftType]);
            Assert.Contains(baseType, hierarchyInfo.DirectParentsMap[leftType]);

            // IRight's parent is IBase
            Assert.True(hierarchyInfo.DirectParentsMap.ContainsKey(rightType));
            Assert.Single(hierarchyInfo.DirectParentsMap[rightType]);
            Assert.Contains(baseType, hierarchyInfo.DirectParentsMap[rightType]);
        }

        /// <summary>
        /// For empty AllCases, empty maps are built
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
        /// For single type (with no parent)
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
        /// Test for complex hierarchy structure
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

            // Animal's children are Mammal and Bird
            Assert.True(hierarchyInfo.DirectChildrenMap.ContainsKey(animalType));
            Assert.Equal(2, hierarchyInfo.DirectChildrenMap[animalType].Count);
            Assert.Contains(mammalType, hierarchyInfo.DirectChildrenMap[animalType]);
            Assert.Contains(birdType, hierarchyInfo.DirectChildrenMap[animalType]);

            // Mammal's children are Dog and Cat
            Assert.True(hierarchyInfo.DirectChildrenMap.ContainsKey(mammalType));
            Assert.Equal(2, hierarchyInfo.DirectChildrenMap[mammalType].Count);
            Assert.Contains(dogType, hierarchyInfo.DirectChildrenMap[mammalType]);
            Assert.Contains(catType, hierarchyInfo.DirectChildrenMap[mammalType]);

            // Dog's parent is Mammal
            Assert.True(hierarchyInfo.DirectParentsMap.ContainsKey(dogType));
            Assert.Single(hierarchyInfo.DirectParentsMap[dogType]);
            Assert.Contains(mammalType, hierarchyInfo.DirectParentsMap[dogType]);

            // Mammal's parent is Animal
            Assert.True(hierarchyInfo.DirectParentsMap.ContainsKey(mammalType));
            Assert.Single(hierarchyInfo.DirectParentsMap[mammalType]);
            Assert.Contains(animalType, hierarchyInfo.DirectParentsMap[mammalType]);
        }

        /// <summary>
        /// Complex case skipping intermediate interfaces
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

            // Don't include IMiddle in AllCases
            var allCases = new HashSet<INamedTypeSymbol>(
                new[] { rootType, leafType, concreteType },
                SymbolEqualityComparer.Default);

            var hierarchyInfo = new ExhaustiveHierarchyInfo(allCases);

            // Concrete's parent is ILeaf (closest)
            Assert.True(hierarchyInfo.DirectParentsMap.ContainsKey(concreteType));
            Assert.Single(hierarchyInfo.DirectParentsMap[concreteType]);
            Assert.Contains(leafType, hierarchyInfo.DirectParentsMap[concreteType]);

            // ILeaf's parent is IRoot (skipping IMiddle)
            Assert.True(hierarchyInfo.DirectParentsMap.ContainsKey(leafType));
            Assert.Single(hierarchyInfo.DirectParentsMap[leafType]);
            Assert.Contains(rootType, hierarchyInfo.DirectParentsMap[leafType]);

            // IRoot has one child (ILeaf via Concrete)
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
