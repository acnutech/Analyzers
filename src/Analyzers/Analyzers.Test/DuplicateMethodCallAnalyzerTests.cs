﻿using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyDuplicateMethodCall = Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixVerifier<
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
        var source = "";

        await VerifyDuplicateMethodCall.VerifyAnalyzerAsync(source);
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

        var expected = VerifyDuplicateMethodCall.Diagnostic(DuplicateMethodCallAnalyzer.Rule)
            .WithLocation(0)
            .WithArguments("MethodB");
        await VerifyDuplicateMethodCall.VerifyCodeFixAsync(source, expected, fixedSource);
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

        var expected = VerifyDuplicateMethodCall.Diagnostic(DuplicateMethodCallAnalyzer.Rule)
            .WithLocation(0)
            .WithArguments("MethodB");
        await VerifyDuplicateMethodCall.VerifyCodeFixAsync(source, expected, fixedSource);
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
                    MethodB(/*c1*/a/*c2*/?/*1a*/  1 /*1b*/:/*c*/2/*c2*/,/*b1*/ 2 /*b2*/);/*i2*/
                }

                void MethodB(int a, int b) {
                }
            }
            """;

        var expected = VerifyDuplicateMethodCall.Diagnostic(DuplicateMethodCallAnalyzer.Rule)
            .WithLocation(0)
            .WithArguments("MethodB");
        await VerifyDuplicateMethodCall.VerifyCodeFixAsync(source, expected, fixedSource);
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

        var expected = VerifyDuplicateMethodCall.Diagnostic(DuplicateMethodCallAnalyzer.Rule)
            .WithLocation(0)
            .WithArguments("MethodB");
        await VerifyDuplicateMethodCall.VerifyCodeFixAsync(source, expected, fixedSource);
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

        var expected = VerifyDuplicateMethodCall.Diagnostic(DuplicateMethodCallAnalyzer.Rule)
            .WithLocation(0)
            .WithArguments("MethodB");
        await VerifyDuplicateMethodCall.VerifyCodeFixAsync(source, expected, fixedSource);
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

        var expected = VerifyDuplicateMethodCall.Diagnostic(DuplicateMethodCallAnalyzer.Rule)
            .WithLocation(0)
            .WithArguments("MethodB");
        await VerifyDuplicateMethodCall.VerifyCodeFixAsync(source, expected, fixedSource);
    }
    
    [TestMethod]
    public async Task IfStatementWithProceedingComment_PreservesIt()
    {
        var source = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = true;
                    // aa
                    {|#0:if|} (a) {
                        // bb
                        MethodB(1);
                    } else {
                        // cc
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
                    // aa
                    MethodB(a ? 1 : 2);
                }

                void MethodB(int a) {
                }
            }
            """;

        var expected = VerifyDuplicateMethodCall.Diagnostic(DuplicateMethodCallAnalyzer.Rule)
            .WithLocation(0)
            .WithArguments("MethodB");
        await VerifyDuplicateMethodCall.VerifyCodeFixAsync(source, expected, fixedSource);
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

        var expected = VerifyDuplicateMethodCall.Diagnostic(DuplicateMethodCallAnalyzer.Rule)
            .WithLocation(0)
            .WithArguments("MethodB");
        await VerifyDuplicateMethodCall.VerifyCodeFixAsync(source, expected, fixedSource);
    }

    [TestMethod]
    public async Task DuplicateCallsInConditional_KeepsFormatting()
    {
        var source = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = true;
                    var e = /*dd*/
                            //dd
                            /* rr */
                            /* rr12
                             rr$ */ 
                            /*aa1*/  a/*a2*/ {|#0:?|}   MethodC/*m2*/(/*1a*/
                                /*ddd*/1 /*1b*/,/*b1*/ 2 /*b2*/)
                        :   MethodC(/*C*/2/*C2*/,
                            2)
                    /*i2*/; /* end */
                }

                int MethodC(int a, int b) {
                    return a;
                }
            }
            """;

        var fixedSource = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = true;
                    var e = /*dd*/
                            //dd
                            /* rr */
                            /* rr12
                             rr$ */
                            MethodC(/*aa1*/  a/*a2*/ ?/*1a*/
                                /*ddd*/1 /*1b*/:/*C*/2/*C2*/,/*b1*/ 2 /*b2*/)
                    /*i2*/; /* end */
                }
            
                int MethodC(int a, int b) {
                    return a;
                }
            }
            """;

        var expected = VerifyDuplicateMethodCall.Diagnostic(DuplicateMethodCallAnalyzer.Rule)
            .WithLocation(0)
            .WithArguments("MethodC");
        await VerifyDuplicateMethodCall.VerifyCodeFixAsync(source, expected, fixedSource);
    }
    
    [TestMethod]
    public async Task DuplicateCallsInConditional_KeepsFormatting2()
    {
        var source = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = true;
                    var e = 
                        a
                        {|#0:?|}  MethodC( 1, 2)
                        : MethodC(2, 2);
                }

                int MethodC(int a, int b) {
                    return a;
                }
            }
            """;

        var fixedSource = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = true;
                    var e =
                        MethodC(a ? 1 : 2, 2);
                }
            
                int MethodC(int a, int b) {
                    return a;
                }
            }
            """;

        var expected = VerifyDuplicateMethodCall.Diagnostic(DuplicateMethodCallAnalyzer.Rule)
            .WithLocation(0)
            .WithArguments("MethodC");
        await VerifyDuplicateMethodCall.VerifyCodeFixAsync(source, expected, fixedSource);
    }
    
    [TestMethod]
    public async Task WithConditionalInsideIfStatement_AppliesCodeFixToConditional()
    {
        var source = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = true;
                    if (a)
                    {
                        MethodC(0);
                    }
                    else
                    {
                        var nn = a {|#0:?|} MethodC(0) : MethodC(3);
                    }
                }

                int MethodC(int a) {
                    return a;
                }
            }
            """;
        
        var fixedSource = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = true;
                    if (a)
                    {
                        MethodC(0);
                    }
                    else
                    {
                        var nn = MethodC(a ? 0 : 3);
                    }
                }
            
                int MethodC(int a) {
                    return a;
                }
            }
            """;

        var expected = VerifyDuplicateMethodCall.Diagnostic(DuplicateMethodCallAnalyzer.Rule)
            .WithLocation(0)
            .WithArguments("MethodC");
        await VerifyDuplicateMethodCall.VerifyCodeFixAsync(source, expected, fixedSource);
    }
    
    [TestMethod]
    public async Task IfStatementWithNotSingleStatement_IsIgnored()
    {
        var source = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = true;
                    if (a)
                    {
                        MethodC(0);
                        MethodC(0);
                    }
                    else
                    {
                        var nn = a {|#0:?|} MethodC(0) : MethodC(3);
                    }
                }

                int MethodC(int a) {
                    return a;
                }
            }
            """;
        
        var fixedSource = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = true;
                    if (a)
                    {
                        MethodC(0);
                        MethodC(0);
                    }
                    else
                    {
                        var nn = MethodC(a ? 0 : 3);
                    }
                }
            
                int MethodC(int a) {
                    return a;
                }
            }
            """;

        var expected = VerifyDuplicateMethodCall.Diagnostic(DuplicateMethodCallAnalyzer.Rule)
            .WithLocation(0)
            .WithArguments("MethodC");
        await VerifyDuplicateMethodCall.VerifyCodeFixAsync(source, expected, fixedSource);
    }

    [TestMethod]
    public async Task DuplicateCalls_OneWithReturn()
    {
        var source = /* lang=c#-test */"""
            class Test
            {
                System.Exception MethodA() {
                    var a = true;
                    {|#0:if|} (a)
                    {
                        MethodB(0);
                    }
                    else
                    {
                        return MethodB(1);
                    }

                    return default;
                }

                System.Exception MethodB(int a) {
                    return default;
                }
            }
            """;

        await VerifyDuplicateMethodCall.VerifyAnalyzerAsync(source);
    }
    
    [TestMethod]
    public async Task DuplicateCalls_OneWithThrow()
    {
        var source = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = true;
                    {|#0:if|} (a)
                    {
                        MethodB(0);
                    }
                    else
                    {
                        throw MethodB(1);
                    }
                }

                System.Exception MethodB(int a) {
                    return default;
                }
            }
            """;

        await VerifyDuplicateMethodCall.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    public async Task DuplicateCalls_WithThrowAndReturn()
    {
        var source = /* lang=c#-test */"""
            class Test
            {
                System.Exception MethodA() {
                    var a = true;
                    {|#0:if|} (a)
                    {
                        throw MethodB(0);
                    }
                    else
                    {
                        return MethodB(1);
                    }
                }

                System.Exception MethodB(int a) {
                    return new System.Exception();
                }
            }
            """;

        await VerifyDuplicateMethodCall.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    public async Task DuplicateCalls_WithThrow()
    {
        var source = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = true;
                    {|#0:if|} (a)
                    {
                        throw MethodB(0);
                    }
                    else
                    {
                        throw MethodB(1);
                    }
                }

                System.Exception MethodB(int a) {
                    return new System.Exception();
                }
            }
            """;
        
        var fixedSource = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = true;
                    throw MethodB(a ? 0 : 1);
                }
            
                System.Exception MethodB(int a) {
                    return new System.Exception();
                }
            }
            """;

        var expected = VerifyDuplicateMethodCall.Diagnostic(DuplicateMethodCallAnalyzer.Rule)
            .WithLocation(0)
            .WithArguments("MethodB");
        await VerifyDuplicateMethodCall.VerifyCodeFixAsync(source, expected, fixedSource);
    }

    [TestMethod]
    public async Task DuplicateCalls_WithReturn()
    {
        var source = /* lang=c#-test */"""
            class Test
            {
                int MethodA() {
                    var a = true;
                    {|#0:if|} (a)
                    {
                        return MethodB(0);
                    }
                    else
                    {
                        return MethodB(1);
                    }
                }

                int MethodB(int a) {
                    return a;
                }
            }
            """;
        
        var fixedSource = /* lang=c#-test */"""
            class Test
            {
                int MethodA() {
                    var a = true;
                    return MethodB(a ? 0 : 1);
                }
            
                int MethodB(int a) {
                    return a;
                }
            }
            """;

        var expected = VerifyDuplicateMethodCall.Diagnostic(DuplicateMethodCallAnalyzer.Rule)
            .WithLocation(0)
            .WithArguments("MethodB");
        await VerifyDuplicateMethodCall.VerifyCodeFixAsync(source, expected, fixedSource);
    }

    [TestMethod]
    public async Task ConditionalDuplicateCalls_WithThrow()
    {
        var source = /* lang=c#-test */"""
            class Test
            {
                void MethodA() {
                    var a = true;
                    var c = a ? throw MethodB(0) : MethodB(1);
                }

                System.Exception MethodB(int a) {
                    return new System.Exception();
                }
            }
            """;

        await VerifyDuplicateMethodCall.VerifyAnalyzerAsync(source);
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

        await VerifyDuplicateMethodCall.VerifyAnalyzerAsync(source);
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

        await VerifyDuplicateMethodCall.VerifyAnalyzerAsync(source);
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

        await VerifyDuplicateMethodCall.VerifyAnalyzerAsync(source);
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

        await VerifyDuplicateMethodCall.VerifyAnalyzerAsync(source);
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

        await VerifyDuplicateMethodCall.VerifyAnalyzerAsync(source);
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

        await VerifyDuplicateMethodCall.VerifyAnalyzerAsync(source);
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

        await VerifyDuplicateMethodCall.VerifyAnalyzerAsync(source);
    }
}
