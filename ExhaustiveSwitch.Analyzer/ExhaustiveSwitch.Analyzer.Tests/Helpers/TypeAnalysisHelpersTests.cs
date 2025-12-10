using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;
using Xunit;

namespace ExhaustiveSwitch.Analyzer.Tests.Helpers
{
    public class TypeAnalysisHelpersTests
    {
        /// <summary>
        /// 属性を持つシンボルを正しく検出
        /// </summary>
        [Fact]
        public void HasAttribute_WithAttribute_ReturnsTrue()
        {
            var code = @"
using System;

[Serializable]
public class MyClass { }
";
            var compilation = CreateCompilation(code);
            var typeSymbol = compilation.GetTypeByMetadataName("MyClass");
            var attributeSymbol = compilation.GetTypeByMetadataName("System.SerializableAttribute");

            var result = TypeAnalysisHelpers.HasAttribute(typeSymbol, attributeSymbol);

            Assert.True(result);
        }

        /// <summary>
        /// 属性を持たないシンボルはfalseを返す
        /// </summary>
        [Fact]
        public void HasAttribute_WithoutAttribute_ReturnsFalse()
        {
            var code = @"
public class MyClass { }
";
            var compilation = CreateCompilation(code);
            var typeSymbol = compilation.GetTypeByMetadataName("MyClass");
            var attributeSymbol = compilation.GetTypeByMetadataName("System.SerializableAttribute");

            var result = TypeAnalysisHelpers.HasAttribute(typeSymbol, attributeSymbol);

            Assert.False(result);
        }

        /// <summary>
        /// インターフェースの実装を正しく検出
        /// </summary>
        [Fact]
        public void IsImplementingOrDerivedFrom_Interface_ReturnsTrue()
        {
            var code = @"
public interface IBase { }

public class Derived : IBase { }
";
            var compilation = CreateCompilation(code);
            var derivedType = compilation.GetTypeByMetadataName("Derived");
            var baseInterface = compilation.GetTypeByMetadataName("IBase");

            var result = TypeAnalysisHelpers.IsImplementingOrDerivedFrom(derivedType, baseInterface);

            Assert.True(result);
        }

        /// <summary>
        /// 基底クラスの継承を正しく検出
        /// </summary>
        [Fact]
        public void IsImplementingOrDerivedFrom_BaseClass_ReturnsTrue()
        {
            var code = @"
public class Base { }

public class Derived : Base { }
";
            var compilation = CreateCompilation(code);
            var derivedType = compilation.GetTypeByMetadataName("Derived");
            var baseClass = compilation.GetTypeByMetadataName("Base");

            var result = TypeAnalysisHelpers.IsImplementingOrDerivedFrom(derivedType, baseClass);

            Assert.True(result);
        }

        /// <summary>
        /// 継承関係がない場合、falseを返す
        /// </summary>
        [Fact]
        public void IsImplementingOrDerivedFrom_Unrelated_ReturnsFalse()
        {
            var code = @"
public class Base { }

public class Unrelated { }
";
            var compilation = CreateCompilation(code);
            var unrelatedType = compilation.GetTypeByMetadataName("Unrelated");
            var baseClass = compilation.GetTypeByMetadataName("Base");

            var result = TypeAnalysisHelpers.IsImplementingOrDerivedFrom(unrelatedType, baseClass);

            Assert.False(result);
        }

        /// <summary>
        /// 同じ型の場合、trueを返す
        /// </summary>
        [Fact]
        public void IsImplementingOrDerivedFrom_SameType_ReturnsTrue()
        {
            var code = @"
public class MyClass { }
";
            var compilation = CreateCompilation(code);
            var typeSymbol = compilation.GetTypeByMetadataName("MyClass");

            var result = TypeAnalysisHelpers.IsImplementingOrDerivedFrom(typeSymbol, typeSymbol);

            Assert.True(result);
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

        private static (Compilation compilation, SemanticModel semanticModel) CreateCompilationWithAttributes(string code)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                new[] { syntaxTree },
                new[]
                {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(ExhaustiveAttribute).Assembly.Location)
                },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            return (compilation, semanticModel);
        }
    }
}
