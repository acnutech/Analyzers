using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyuplicateMethodCall = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Acnutech.Analyzers.DuplicateMethodCallAnalyzer, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Acnutech.Analyzers.Test;

[TestClass]
public class DuplicateMethodCallAnalyzerTests
{
    [TestMethod]
    public async Task EmptyFile_IsIgnored()
    {
        var test = @"";

        await VerifyuplicateMethodCall.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    public async Task DuplicateCalls_InBlocks()
    {
        var test = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = true;
                    {|#0:if|} (a) {
                        MethodB(1);
                    } else {
                        MethodB(2);
                    }
                }

                void MethodB(int a) {
                }
            }
            """;

        var expected = VerifyuplicateMethodCall.Diagnostic(DuplicateMethodCallAnalyzer.Rule)
            .WithLocation(0)
            .WithArguments("MethodB");
        await VerifyuplicateMethodCall.VerifyAnalyzerAsync(test, expected);
    }

    [TestMethod]
    public async Task DuplicateCalls_Bare()
    {
        var test = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = true;
                    {|#0:if|} (a)
                        MethodB(1);
                    else {
                        MethodB(2);
                    }
                }

                void MethodB(int a) {
                }
            }
            """;

        var expected = VerifyuplicateMethodCall.Diagnostic(DuplicateMethodCallAnalyzer.Rule)
            .WithLocation(0)
            .WithArguments("MethodB");
        await VerifyuplicateMethodCall.VerifyAnalyzerAsync(test, expected);
    }

    [TestMethod]
    public async Task DuplicateCalls_WithIgnoredDifferences()
    {
        var test = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = true;
                    {|#0:if|} (a) {
                        MethodB(1,/*a*/ 2);
                    } else {
                        MethodB(2, 
                        2);
                    }
                }

                void MethodB(int a, int b) {
                }
            }
            """;

        var expected = VerifyuplicateMethodCall.Diagnostic(DuplicateMethodCallAnalyzer.Rule)
            .WithLocation(0)
            .WithArguments("MethodB");
        await VerifyuplicateMethodCall.VerifyAnalyzerAsync(test, expected);
    }

    [TestMethod]
    public async Task DiffeentNumberOfArguments()
    {
        var test = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = true;
                    {|#0:if|} (a) {
                        MethodB(1);
                    } else {
                        MethodB(2, 2);
                    }
                }

                void MethodB(int a, int b = 0) {
                }
            }
            """;

        await VerifyuplicateMethodCall.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    public async Task DiffeentOverloads()
    {
        var test = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = true;
                    {|#0:if|} (a) {
                        MethodB(1, 3.0);
                    } else {
                        MethodB(2, 2);
                    }
                }

                void MethodB(int a, double d) {
                }

                void MethodB(int a, int b) {
                }
            }
            """;

        await VerifyuplicateMethodCall.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    public async Task MoreThanOneDifferentArgument()
    {
        var test = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = true;
                    {|#0:if|} (a) {
                        MethodB(1, 1);
                    } else {
                        MethodB(2, 2);
                    }
                }

                void MethodB(int a, int b) {
                }
            }
            """;

        await VerifyuplicateMethodCall.VerifyAnalyzerAsync(test);
    }
}
