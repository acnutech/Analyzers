using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyOutParameterToReturn = Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixVerifier<
    Acnutech.Analyzers.OutParameterToReturnAnalyzer,
    Acnutech.Analyzers.OutParameterToReturnCodeFixProvider,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Acnutech.Analyzers.Test;

[TestClass]
public class OutParameterToReturnAnalyzerTests
{
    [TestMethod]
    public async Task EmptyFile_IsIgnored()
    {
        var test = "";

        await VerifyOutParameterToReturn.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    public async Task MethodWithSingleOutParameter_IsReported()
    {
        var test = /* lang=c#-test */"""
            class Test
            {
                void MethodA({|#0:out|} int a)
                {
                    a = 1;
                }
            }
            """;

        var fixedSource = /* lang=c#-test */"""
            class Test
            {
                int MethodA()
                {
                    int a;
                    a = 1;
                    return a;
                }
            }
            """;

        var expected = VerifyOutParameterToReturn.Diagnostic(OutParameterToReturnAnalyzer.Rule).WithLocation(0).WithArguments("MethodA");
        await VerifyOutParameterToReturn.VerifyCodeFixAsync(test, expected, fixedSource);
    }

    [TestMethod]
    public async Task MethodWithOtherNonOutParameters_IsReported()
    {
        var test = /* lang=c#-test */"""
            class Test
            {
                void MethodA(string s, {|#0:out|} int a)
                {
                    a = 1;
                }
            }
            """;

        var fixedSource = /* lang=c#-test */"""
            class Test
            {
                int MethodA(string s)
                {
                    int a;
                    a = 1;
                    return a;
                }
            }
            """;

        var expected = VerifyOutParameterToReturn.Diagnostic(OutParameterToReturnAnalyzer.Rule).WithLocation(0).WithArguments("MethodA");
        await VerifyOutParameterToReturn.VerifyCodeFixAsync(test, expected, fixedSource);
    }

    [TestMethod]
    public async Task CallSides_AreUpdatedCorrectly()
    {
        var test = /* lang=c#-test */"""
            class Test
            {
                void MethodA(int d, {|#0:out|} int a)
                {
                    a = 1;
                }

                void MethodB()
                {
                    int a;
                    MethodA(5, out a);
                }
            }
            """;

        var fixedSource = /* lang=c#-test */"""
            class Test
            {
                int MethodA(int d)
                {
                    int a;
                    a = 1;
                    return a;
                }
            
                void MethodB()
                {
                    int a;
                    a = MethodA(5);
                }
            }
            """;

        var expected = VerifyOutParameterToReturn.Diagnostic(OutParameterToReturnAnalyzer.Rule).WithLocation(0).WithArguments("MethodA");
        await VerifyOutParameterToReturn.VerifyCodeFixAsync(test, expected, fixedSource);
    }

    [TestMethod]
    public async Task MethodWithNoOutParameters_IsIgnored()
    {
        var test = /* lang=c#-test */"""
            class Test
            {
                void MethodA(string s, int a)
                {
                  a = 1;
                }
            }
            """;

        await VerifyOutParameterToReturn.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    public async Task MethodWithMultipleOutParameters_IsIgnored()
    {
        var test = /* lang=c#-test */"""
            class Test
            {
                void MethodA(out string s, out int a)
                {
                  a = 1;
                  s = "";
                }
            }
            """;

        await VerifyOutParameterToReturn.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    public async Task AbstractMethod_IsIgnored()
    {
        var test = /* lang=c#-test */"""
            abstract class Test
            {
                public abstract void MethodA(out string s);
            }
            """;

        await VerifyOutParameterToReturn.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    public async Task VirtualMethod_IsIgnored()
    {
        var test = /* lang=c#-test */"""
            class Test
            {
                public virtual void MethodA(out string s)
                {
                    s = "";
                }
            }
            """;

        await VerifyOutParameterToReturn.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    public async Task OverrideMethod_IsIgnored()
    {
        var test = /* lang=c#-test */"""
            abstract class TestA
            {
                public abstract void MethodA(out string s);
            }
            
            class TestB : TestA
            {
                public override void MethodA(out string s)
                {
                    s = "";
                }
            }
            """;

        await VerifyOutParameterToReturn.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    public async Task MethodImplementingInterface_IsIgnored()
    {
        var test = /* lang=c#-test */"""
            interface ITestA
            {
                void MethodA(out string s);
            }
            
            class TestB : ITestA
            {
                public void MethodA(out string s)
                {
                    s = "";
                }
            }
            """;

        await VerifyOutParameterToReturn.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    public async Task MethodImplementingInterfaceExplicitly_IsIgnored()
    {
        var test = /* lang=c#-test */"""
            interface ITestA
            {
                void MethodA(out string s);
            }
            
            class TestB : ITestA
            {
                void ITestA.MethodA(out string s)
                {
                    s = "";
                }
            }
            """;

        await VerifyOutParameterToReturn.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    public async Task MethodWithNonVoidReturnType_IsIgnored()
    {
        var test = /* lang=c#-test */"""
            class Test
            {
                public virtual int MethodA(out string s)
                {
                    s = "";
                    return 0;
                }
            }
            """;

        await VerifyOutParameterToReturn.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    public async Task MethodWithAttributedOutParameter_IsIgnored()
    {
        var test = /* lang=c#-test */"""
            class Test
            {
                public void MethodA([System.ComponentModel.Description("")] out string s)
                {
                    s = "";
                }
            }
            """;

        await VerifyOutParameterToReturn.VerifyAnalyzerAsync(test);
    }
}
