using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace ExhaustiveSwitch.Analyzer.Tests.Helpers
{
    public class MetadataHelpersTests
    {
        /// <summary>
        /// 単純な型の完全なメタデータ名を取得
        /// </summary>
        [Fact]
        public void GetFullMetadataName_SimpleType_ReturnsFullName()
        {
            var code = @"
namespace MyNamespace
{
    public class MyClass { }
}";
            var (compilation, semanticModel) = CreateCompilation(code);
            var classSymbol = GetTypeSymbol(compilation, "MyNamespace.MyClass");

            var result = MetadataHelpers.GetFullMetadataName(classSymbol);

            Assert.Equal("MyNamespace.MyClass", result);
        }

        /// <summary>
        /// ネストされた型の完全なメタデータ名を取得
        /// </summary>
        [Fact]
        public void GetFullMetadataName_NestedType_ReturnsFullNameWithPlus()
        {
            var code = @"
namespace MyNamespace
{
    public class OuterClass
    {
        public class InnerClass { }
    }
}";
            var (compilation, semanticModel) = CreateCompilation(code);
            var classSymbol = GetTypeSymbol(compilation, "MyNamespace.OuterClass+InnerClass");

            var result = MetadataHelpers.GetFullMetadataName(classSymbol);

            Assert.Equal("MyNamespace.OuterClass+InnerClass", result);
        }

        /// <summary>
        /// 名前空間がない型の完全なメタデータ名を取得
        /// </summary>
        [Fact]
        public void GetFullMetadataName_NoNamespace_ReturnsTypeName()
        {
            var code = @"
public class MyClass { }
";
            var (compilation, semanticModel) = CreateCompilation(code);
            var classSymbol = GetTypeSymbol(compilation, "MyClass");

            var result = MetadataHelpers.GetFullMetadataName(classSymbol);

            Assert.Equal("MyClass", result);
        }

        /// <summary>
        /// null型の場合は空文字列を返す
        /// </summary>
        [Fact]
        public void GetFullMetadataName_NullType_ReturnsEmpty()
        {
            var result = MetadataHelpers.GetFullMetadataName(null);

            Assert.Equal(string.Empty, result);
        }

        /// <summary>
        /// 名前空間の完全な名前を取得
        /// </summary>
        [Fact]
        public void GetNamespaceName_NestedNamespace_ReturnsFullName()
        {
            var code = @"
namespace Outer.Inner
{
    public class MyClass { }
}";
            var (compilation, semanticModel) = CreateCompilation(code);
            var classSymbol = GetTypeSymbol(compilation, "Outer.Inner.MyClass");
            var namespaceSymbol = classSymbol.ContainingNamespace;

            var result = MetadataHelpers.GetNamespaceName(namespaceSymbol);

            Assert.Equal("Outer.Inner", result);
        }

        /// <summary>
        /// グローバル名前空間の場合は空文字列を返す
        /// </summary>
        [Fact]
        public void GetNamespaceName_GlobalNamespace_ReturnsEmpty()
        {
            var code = @"
public class MyClass { }
";
            var (compilation, semanticModel) = CreateCompilation(code);
            var classSymbol = GetTypeSymbol(compilation, "MyClass");
            var namespaceSymbol = classSymbol.ContainingNamespace;

            var result = MetadataHelpers.GetNamespaceName(namespaceSymbol);

            Assert.Equal(string.Empty, result);
        }

        /// <summary>
        /// nullの名前空間の場合は空文字列を返す
        /// </summary>
        [Fact]
        public void GetNamespaceName_Null_ReturnsEmpty()
        {
            var result = MetadataHelpers.GetNamespaceName(null);

            Assert.Equal(string.Empty, result);
        }

        private static (Compilation compilation, SemanticModel semanticModel) CreateCompilation(string code)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                new[] { syntaxTree },
                new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            return (compilation, semanticModel);
        }

        private static INamedTypeSymbol GetTypeSymbol(Compilation compilation, string metadataName)
        {
            return compilation.GetTypeByMetadataName(metadataName);
        }
    }
}
