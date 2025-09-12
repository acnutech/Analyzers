using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifRedundantConvert = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Acnutech.Analyzers.RedundantConvertAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Acnutech.Analyzers.Test;

[TestClass]
public class RedundantConvertAnalyzerTests
{
    [TestMethod]
    public async Task EmptyFile_IsIgnored()
    {
        var source = @"";

        await VerifRedundantConvert.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    public async Task ReportsDiagnostic_ForRedundantConvertToInt32()
    {
        var source = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = {|#0:System.Convert.ToInt32|}(4);
                }
            }
            """;

        var expected = VerifRedundantConvert.Diagnostic(RedundantConvertAnalyzer.Rule)
            .WithLocation(0)
            .WithArguments("ToInt32", "int");
        await VerifRedundantConvert.VerifyAnalyzerAsync(source, expected);
    }

    [TestMethod]
    public async Task ReportsDiagnostic_ForRedundantConvertToDouble_LiteralArgument()
    {
        var source = /* lang=c#-test */"""
            using System;
            class Test
            {
                void MethodA() {
                    var a = {|#0:Convert.ToDouble|}(3.0);
                }
            }
            """;

        var expected = VerifRedundantConvert.Diagnostic(RedundantConvertAnalyzer.Rule)
            .WithLocation(0)
            .WithArguments("ToDouble", "double");
        await VerifRedundantConvert.VerifyAnalyzerAsync(source, expected);
    }

    [TestMethod]
    public async Task ReportsDiagnostic_ForRedundantConvertToDouble_ExpressionArgument()
    {
        var source = /* lang=c#-test */"""
            using System;
            class Test
            {
                void MethodA() {
                    var a = 3.0;
                    var b = {|#0:Convert.ToDouble|}(a + 3);
                }
            }
            """;

        var expected = VerifRedundantConvert.Diagnostic(RedundantConvertAnalyzer.Rule)
            .WithLocation(0)
            .WithArguments("ToDouble", "double");
        await VerifRedundantConvert.VerifyAnalyzerAsync(source, expected);
    }

    [TestMethod]
    public async Task DoesNotReportDiagnostic_ForConvertToDouble_WithNonRedundantArgument()
    {
        var source = /* lang=c#-test */"""
            using System;
            class Test
            {
                void MethodA() {
                    var a = Convert.ToDouble(5);
                }
            }
            """;

        await VerifRedundantConvert.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    public async Task DoesNotReportDiagnostic_ForConvertToString_WithNullArgument()
    {
        var source = /* lang=c#-test */"""
            using System;
            class Test
            {
                void MethodA() {
                    var a = Convert.ToString(null);
                }
            }
            """;

        await VerifRedundantConvert.VerifyAnalyzerAsync(source);
    }
}
