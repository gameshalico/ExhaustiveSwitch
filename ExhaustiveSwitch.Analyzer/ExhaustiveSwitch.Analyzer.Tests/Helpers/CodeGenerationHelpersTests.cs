using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace ExhaustiveSwitch.Analyzer.Tests.Helpers
{
    public class CodeGenerationHelpersTests
    {
        /// <summary>
        /// Generate variable name from normal type name
        /// </summary>
        [Fact]
        public void GetVariableName_NormalType_ReturnsLowerCamelCase()
        {
            var typeSymbol = CreateTypeSymbol("MyClass");

            var result = CodeGenerationHelpers.GetVariableName(typeSymbol);

            Assert.Equal("myClass", result);
        }

        /// <summary>
        /// Generate variable name from single character type name
        /// </summary>
        [Fact]
        public void GetVariableName_SingleCharacter_ReturnsLowerCase()
        {
            var typeSymbol = CreateTypeSymbol("A");

            var result = CodeGenerationHelpers.GetVariableName(typeSymbol);

            Assert.Equal("a", result);
        }

        /// <summary>
        /// For type name same as keyword, add @
        /// </summary>
        [Fact]
        public void GetVariableName_Keyword_ReturnsWithAt()
        {
            var typeSymbol = CreateTypeSymbol("Class");

            var result = CodeGenerationHelpers.GetVariableName(typeSymbol);

            Assert.Equal("@class", result);
        }

        /// <summary>
        /// For type name same as keyword (string), add @
        /// </summary>
        [Fact]
        public void GetVariableName_String_ReturnsWithAt()
        {
            var typeSymbol = CreateTypeSymbol("String");

            var result = CodeGenerationHelpers.GetVariableName(typeSymbol);

            Assert.Equal("@string", result);
        }

        /// <summary>
        /// For type name starting with lowercase, return as is
        /// </summary>
        [Fact]
        public void GetVariableName_LowerCaseStart_ReturnsSame()
        {
            var typeSymbol = CreateTypeSymbol("myType");

            var result = CodeGenerationHelpers.GetVariableName(typeSymbol);

            Assert.Equal("myType", result);
        }

        /// <summary>
        /// For type name with consecutive capitals, lowercase only first
        /// </summary>
        [Fact]
        public void GetVariableName_AllCaps_ReturnsLowerFirst()
        {
            var typeSymbol = CreateTypeSymbol("HTTPClient");

            var result = CodeGenerationHelpers.GetVariableName(typeSymbol);

            Assert.Equal("hTTPClient", result);
        }

        /// <summary>
        /// For null type, return default value
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
