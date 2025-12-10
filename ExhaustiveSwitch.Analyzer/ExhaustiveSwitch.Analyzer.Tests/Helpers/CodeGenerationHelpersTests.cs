using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace ExhaustiveSwitch.Analyzer.Tests.Helpers
{
    public class CodeGenerationHelpersTests
    {
        /// <summary>
        /// 通常の型名から変数名を生成
        /// </summary>
        [Fact]
        public void GetVariableName_NormalType_ReturnsLowerCamelCase()
        {
            var typeSymbol = CreateTypeSymbol("MyClass");

            var result = CodeGenerationHelpers.GetVariableName(typeSymbol);

            Assert.Equal("myClass", result);
        }

        /// <summary>
        /// 1文字の型名から変数名を生成
        /// </summary>
        [Fact]
        public void GetVariableName_SingleCharacter_ReturnsLowerCase()
        {
            var typeSymbol = CreateTypeSymbol("A");

            var result = CodeGenerationHelpers.GetVariableName(typeSymbol);

            Assert.Equal("a", result);
        }

        /// <summary>
        /// 予約語と同じ型名の場合、@をつける
        /// </summary>
        [Fact]
        public void GetVariableName_Keyword_ReturnsWithAt()
        {
            var typeSymbol = CreateTypeSymbol("Class");

            var result = CodeGenerationHelpers.GetVariableName(typeSymbol);

            Assert.Equal("@class", result);
        }

        /// <summary>
        /// 予約語と同じ型名の場合（string）、@をつける
        /// </summary>
        [Fact]
        public void GetVariableName_String_ReturnsWithAt()
        {
            var typeSymbol = CreateTypeSymbol("String");

            var result = CodeGenerationHelpers.GetVariableName(typeSymbol);

            Assert.Equal("@string", result);
        }

        /// <summary>
        /// 小文字で始まる型名の場合、そのまま返す
        /// </summary>
        [Fact]
        public void GetVariableName_LowerCaseStart_ReturnsSame()
        {
            var typeSymbol = CreateTypeSymbol("myType");

            var result = CodeGenerationHelpers.GetVariableName(typeSymbol);

            Assert.Equal("myType", result);
        }

        /// <summary>
        /// 大文字が続く型名の場合、最初だけ小文字化
        /// </summary>
        [Fact]
        public void GetVariableName_AllCaps_ReturnsLowerFirst()
        {
            var typeSymbol = CreateTypeSymbol("HTTPClient");

            var result = CodeGenerationHelpers.GetVariableName(typeSymbol);

            Assert.Equal("hTTPClient", result);
        }

        /// <summary>
        /// nullの型の場合、デフォルト値を返す
        /// </summary>
        [Fact]
        public void GetVariableName_NullType_ReturnsDefault()
        {
            var result = CodeGenerationHelpers.GetVariableName(null!);

            Assert.Equal("value", result);
        }

        private static INamedTypeSymbol CreateTypeSymbol(string typeName)
        {
            var code = $@"
public class {typeName} {{ }}
";
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                new[] { syntaxTree },
                new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

            return compilation.GetTypeByMetadataName(typeName)!;
        }
    }
}
