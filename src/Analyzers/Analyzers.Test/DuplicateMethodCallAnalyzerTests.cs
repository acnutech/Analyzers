using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyuplicateMethodCall = Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixVerifier<
    Acnutech.Analyzers.DuplicateMethodCallAnalyzer,
    Acnutech.Analyzers.DuplicateMethodCallCodeFixProvider,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Acnutech.Analyzers.Test;

[TestClass]
public class DuplicateMethodCallAnalyzerTests
{
    [TestMethod]
    public async Task EmptyFile_IsIgnored()
    {
        var source = @"";

        await VerifyuplicateMethodCall.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    public async Task DuplicateCalls_InBlocks()
    {
        var source = /* lang=c#-test */"""
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

        var fixedSource = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = true;
                    MethodB(a ? 1 : 2);
                }

                void MethodB(int a) {
                }
            }
            """;

        var expected = VerifyuplicateMethodCall.Diagnostic(DuplicateMethodCallAnalyzer.Rule)
            .WithLocation(0)
            .WithArguments("MethodB");
        await VerifyuplicateMethodCall.VerifyCodeFixAsync(source, expected, fixedSource);
    }

    [TestMethod]
    public async Task DuplicateCalls_Bare()
    {
        var source = /* lang=c#-test */"""
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

        var fixedSource = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = true;
                    MethodB(a ? 1 : 2);
                }

                void MethodB(int a) {
                }
            }
            """;

        var expected = VerifyuplicateMethodCall.Diagnostic(DuplicateMethodCallAnalyzer.Rule)
            .WithLocation(0)
            .WithArguments("MethodB");
        await VerifyuplicateMethodCall.VerifyCodeFixAsync(source, expected, fixedSource);
    }

    [TestMethod]
    public async Task DuplicateCalls_WithIgnoredDifferences()
    {
        var source = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = true;
                    {|#0:if|} (/*c1*/a/*c2*/)
                    {
                        /*m1*/ MethodB/*m2*/(/*1a*/  1 /*1b*/,/*b1*/ 2 /*b2*/);
                    }
                    else
                    {
                        MethodB(/*c*/2/*c2*/,
                        2);
                    }/*i2*/
                }

                void MethodB(int a, int b) {
                }
            }
            """;

        var fixedSource = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = true;
                    MethodB(/*c1*/a/*c2*/?/*1a*/  1 /*1b*/:/*c*/2/*c2*/,/*b1*/ 2 /*b2*/);
                }

                void MethodB(int a, int b) {
                }
            }
            """;

        var expected = VerifyuplicateMethodCall.Diagnostic(DuplicateMethodCallAnalyzer.Rule)
            .WithLocation(0)
            .WithArguments("MethodB");
        await VerifyuplicateMethodCall.VerifyCodeFixAsync(source, expected, fixedSource);
    }

    [TestMethod]
    public async Task DuplicateCalls_WithOptionalArguments()
    {
        var source = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = true;
                    {|#0:if|} (a) {
                        MethodB(1, c: 2);
                    } else {
                        MethodB(2, c: 2);
                    }
                }

                void MethodB(int a, int b = 3, int c = 4) {
                }
            }
            """;

        var fixedSource = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = true;
                    MethodB(a ? 1 : 2, c: 2);
                }

                void MethodB(int a, int b = 3, int c = 4) {
                }
            }
            """;

        var expected = VerifyuplicateMethodCall.Diagnostic(DuplicateMethodCallAnalyzer.Rule)
            .WithLocation(0)
            .WithArguments("MethodB");
        await VerifyuplicateMethodCall.VerifyCodeFixAsync(source, expected, fixedSource);
    }

    [TestMethod]
    public async Task DuplicateCalls_WithChangedOptionalArgument()
    {
        var source = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = true;
                    {|#0:if|} (a) {
                        MethodB(1, c: 1);
                    } else {
                        MethodB(1, c: 2);
                    }
                }

                void MethodB(int a, int b = 3, int c = 4) {
                }
            }
            """;

        var fixedSource = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = true;
                    MethodB(1, c: a ? 1 : 2);
                }

                void MethodB(int a, int b = 3, int c = 4) {
                }
            }
            """;

        var expected = VerifyuplicateMethodCall.Diagnostic(DuplicateMethodCallAnalyzer.Rule)
            .WithLocation(0)
            .WithArguments("MethodB");
        await VerifyuplicateMethodCall.VerifyCodeFixAsync(source, expected, fixedSource);
    }

    [TestMethod]
    public async Task DuplicateCalls_WithRefArguments()
    {
        var source = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = true;
                    var b = 2;
                    {|#0:if|} (a) {
                        MethodB(1, ref b);
                    } else {
                        MethodB(2, ref b);
                    }
                }

                void MethodB(int a, ref int b) {
                }
            }
            """;

        var fixedSource = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = true;
                    var b = 2;
                    MethodB(a ? 1 : 2, ref b);
                }

                void MethodB(int a, ref int b) {
                }
            }
            """;

        var expected = VerifyuplicateMethodCall.Diagnostic(DuplicateMethodCallAnalyzer.Rule)
            .WithLocation(0)
            .WithArguments("MethodB");
        await VerifyuplicateMethodCall.VerifyCodeFixAsync(source, expected, fixedSource);
    }
    
    [TestMethod]
    public async Task DuplicateCallsInConditional_Bare()
    {
        var source = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = true;
                    var r = a {|#0:?|} MethodB(1) : MethodB(2);
                }

                int MethodB(int a) {
                    return a;
                }
            }
            """;

        var fixedSource = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = true;
                    var r = MethodB(a ? 1 : 2);
                }

                int MethodB(int a) {
                    return a;
                }
            }
            """;

        var expected = VerifyuplicateMethodCall.Diagnostic(DuplicateMethodCallAnalyzer.Rule)
            .WithLocation(0)
            .WithArguments("MethodB");
        await VerifyuplicateMethodCall.VerifyCodeFixAsync(source, expected, fixedSource);
    }

    [TestMethod]
    public async Task NoArguments()
    {
        var source = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = true;
                    {|#0:if|} (a) {
                        MethodB();
                    } else {
                        MethodB();
                    }
                }

                void MethodB() {
                }
            }
            """;

        await VerifyuplicateMethodCall.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    public async Task WithNoDifferences()
    {
        var source = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = true;
                    {|#0:if|} (a) {
                        MethodB(1, 2);
                    } else {
                        MethodB(1, 2);
                    }
                }

                void MethodB(int a, int b = 1, int c = 1) {
                }
            }
            """;

        await VerifyuplicateMethodCall.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    public async Task DifferentNumberOfArguments()
    {
        var source = /* lang=c#-test */"""
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

        await VerifyuplicateMethodCall.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    public async Task DifferentOverloads()
    {
        var source = /* lang=c#-test */"""
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

        await VerifyuplicateMethodCall.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    public async Task MoreThanOneDifferentArgument()
    {
        var source = /* lang=c#-test */"""
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

        await VerifyuplicateMethodCall.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    public async Task DifferentOptionalArgumentsAreUsed()
    {
        var source = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = true;
                    {|#0:if|} (a) {
                        MethodB(1, b: 1);
                    } else {
                        MethodB(1, c: 1);
                    }
                }

                void MethodB(int a, int b = 1, int c = 1) {
                }
            }
            """;

        await VerifyuplicateMethodCall.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    public async Task NotMatchingOptionalArguments()
    {
        var source = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = true;
                    {|#0:if|} (a) {
                        MethodB(1, b: 1);
                    } else {
                        MethodB(1, 1);
                    }
                }

                void MethodB(int a, int b = 1, int c = 1) {
                }
            }
            """;

        await VerifyuplicateMethodCall.VerifyAnalyzerAsync(source);
    }
}
