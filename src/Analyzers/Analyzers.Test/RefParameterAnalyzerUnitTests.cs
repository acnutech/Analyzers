using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixVerifier<
    Acnutech.Analyzers.RefParameterAnalyzer,
    Acnutech.Analyzers.RefParameterAnalyzerCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Acnutech.Analyzers.Test;

[TestClass]
public class RefParameterAnalyzerUnitTest
{
    [TestMethod]
    public async Task EmptyFile_IsIgnored()
    {
        var test = @"";

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    public async Task RefParameter_IsFixable()
    {
        var test = /* lang=c#-test */@"
    namespace ConsoleApplication1
    {
        class Test
        {
            void MethodA({|#0:ref|} int a) { System.Console.WriteLine(a); }
        }
    }";

        var codeFixTest = /* lang=c#-test */@"
    namespace ConsoleApplication1
    {
        class Test
        {
            void MethodA(int a) { System.Console.WriteLine(a); }
        }
    }";

        var expected = VerifyCS.Diagnostic(RefParameterAnalyzer.RemoveUnnecessaryRefModifierDiagnostic.Rule).WithLocation(0).WithArguments("a");
        await VerifyCS.VerifyCodeFixAsync(test, expected, codeFixTest);
    }

    [TestMethod]
    public async Task FixUsagesOfMethodWithRefParameters()
    {
        var test = /* lang=c#-test */@"
    namespace ConsoleApplication1
    {
        class Test
        {
            void MethodA({|#0:ref|} int a) {}

            void MethodB()
            {
                int b = 0;
                MethodA(ref b);
            }
        }
    }";

        var codeFixTest = /* lang=c#-test */@"
    namespace ConsoleApplication1
    {
        class Test
        {
            void MethodA(int a) {}

            void MethodB()
            {
                int b = 0;
                MethodA(b);
            }
        }
    }";

        var expected = VerifyCS.Diagnostic(RefParameterAnalyzer.RemoveUnnecessaryRefModifierDiagnostic.Rule).WithLocation(0).WithArguments("a");
        await VerifyCS.VerifyCodeFixAsync(test, expected, codeFixTest);
    }

    [TestMethod]
    public async Task PreserveIndentation()
    {
        var test = /* lang=c#-test */@"
    namespace ConsoleApplication1
    {
        class Test
        {
            void MethodA(int a, 
                         {|#0:ref|} int b,
                         int c) { c = b; }

            void MethodB()
            {
                int b = 0;
                MethodA(1,
                    ref b, 3);
            }
        }
    }";

        var codeFixTest = /* lang=c#-test */@"
    namespace ConsoleApplication1
    {
        class Test
        {
            void MethodA(int a,
                         int b,
                         int c) { c = b; }

            void MethodB()
            {
                int b = 0;
                MethodA(1,
                    b, 3);
            }
        }
    }";

        var expected = VerifyCS.Diagnostic(RefParameterAnalyzer.RemoveUnnecessaryRefModifierDiagnostic.Rule).WithLocation(0).WithArguments("b");
        await VerifyCS.VerifyCodeFixAsync(test, expected, codeFixTest);
    }

    [TestMethod]
    public async Task PreserveTrivia()
    {
        var test = /* lang=c#-test */@"
    namespace ConsoleApplication1
    {
        class Test
        {
            void MethodA(int a, 
                         {|#0:ref|} /*a*/int b,
                         int c) { c = b; }

            void MethodB()
            {
                int b = 0;
                MethodA(1,
                    /*d*/ref/*c*/ b, 3);
            }
        }
    }";

        var codeFixTest = /* lang=c#-test */@"
    namespace ConsoleApplication1
    {
        class Test
        {
            void MethodA(int a,
                          /*a*/int b,
                         int c) { c = b; }

            void MethodB()
            {
                int b = 0;
                MethodA(1,
                    /*d*//*c*/ b, 3);
            }
        }
    }";

        var expected = VerifyCS.Diagnostic(RefParameterAnalyzer.RemoveUnnecessaryRefModifierDiagnostic.Rule).WithLocation(0).WithArguments("b");
        await VerifyCS.VerifyCodeFixAsync(test, expected, codeFixTest);
    }

    [TestMethod]
    public async Task FormatModifiedFragments()
    {
        var test = /* lang=c#-test */@"
    namespace ConsoleApplication1
    {
        class Test
        {
            void MethodA(int a,  {|#0:ref|}  int b, int c) {}

            void MethodB()
            {
                int b = 0;
                MethodA(1,  ref  b, 3);
            }
        }
    }";

        var codeFixTest = /* lang=c#-test */@"
    namespace ConsoleApplication1
    {
        class Test
        {
            void MethodA(int a, int b, int c) {}

            void MethodB()
            {
                int b = 0;
                MethodA(1, b, 3);
            }
        }
    }";

        var expected = VerifyCS.Diagnostic(RefParameterAnalyzer.RemoveUnnecessaryRefModifierDiagnostic.Rule).WithLocation(0).WithArguments("b");
        await VerifyCS.VerifyCodeFixAsync(test, expected, codeFixTest);
    }

    [TestMethod]
    public async Task UpdateMultipleReferencesToChangedMethod()
    {
        var test = /* lang=c#-test */@"
    namespace ConsoleApplication1
    {
        class Test
        {
            void MethodA({|#0:ref|} int a) {}

            void MethodB()
            {
                int b = 0;
                MethodA(ref b);
                MethodA(ref b);
                MethodA(ref b);
                MethodA(ref b);
                MethodA(ref b);
                MethodA(ref b);
            }
        }
    }";

        var codeFixTest = /* lang=c#-test */@"
    namespace ConsoleApplication1
    {
        class Test
        {
            void MethodA(int a) {}

            void MethodB()
            {
                int b = 0;
                MethodA(b);
                MethodA(b);
                MethodA(b);
                MethodA(b);
                MethodA(b);
                MethodA(b);
            }
        }
    }";

        var expected = VerifyCS.Diagnostic(RefParameterAnalyzer.RemoveUnnecessaryRefModifierDiagnostic.Rule).WithLocation(0).WithArguments("a");
        await VerifyCS.VerifyCodeFixAsync(test, expected, codeFixTest);
    }

    [TestMethod]
    public async Task OverridingMethod_IsOmitted()
    {
        var test = /* lang=c#-test */@"
    namespace ConsoleApplication1
    {
        abstract class Base {
            public abstract void MethodA(ref int a);
        }

        class Test : Base
        {
            public override void MethodA(ref int a) {
            }
        }
    }";

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    public async Task MethodExplicitlyImplementsInterface_IsOmitted()
    {
        var test = /* lang=c#-test */@"
    namespace ConsoleApplication1
    {
        interface IBase {
            public void MethodA(ref int a);
        }

        class Test : IBase
        {
            public void MethodA(ref int a) {
            }
        }
    }";

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    public async Task MethodImplementsInterface_IsOmitted()
    {
        var test = /* lang=c#-test */@"
    namespace ConsoleApplication1
    {
        interface IBase {
            public void MethodA(ref int a);
        }

        class Test : IBase
        {
            void IBase.MethodA(ref int a) {
            }
        }
    }";

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    public async Task AssignedRefModifier_IsOmitted()
    {
        var test = /* lang=c#-test */@"
    namespace ConsoleApplication1
    {
        class Test
        {
            void MethodA(ref int a) {
                a = a;
            }
        }
    }";

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    public async Task VirtualMethod_IsOmitted()
    {
        var test = /* lang=c#-test */@"
    namespace ConsoleApplication1
    {
        class Test
        {
            public virtual void MethodA(ref int a) {
            }
        }
    }";

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    public async Task RefIsUsedAsRefForAnotherCall_IsOmitted()
    {
        var test = /* lang=c#-test */@"
        class Test
        {
            void MethodA(ref bool a) {
                System.Threading.Monitor.TryEnter(new object(), ref a);
            }
}";

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    public async Task MultipleModifiers_IsOmitted()
    {
        var source = /* lang=c#-test */"""
            class Test
            {
                void MethodA(ref params bool[] a) {
                }
            }
            """;

        await new CSharpAnalyzerTest<RefParameterAnalyzer, DefaultVerifier>
        {
            DisabledDiagnostics = { "CS8328" },
            TestCode = source,
            ExpectedDiagnostics =
            {
                // Adding to DisabledDiagnostics does not work
                DiagnosticResult.CompilerError("CS8328").WithSpan(3, 22, 3, 28).WithArguments("params", "ref"),
            }

        }.RunAsync();
    }

    [TestMethod]
    public async Task RefParameterToOut_IsFixable()
    {
        var test = /* lang=c#-test */"""
            namespace ConsoleApplication1
            {
                class Test
                {
                    void MethodA({|#0:ref|} int a) {
                        a = 0;
                    }
                }
            }
            """;

        var expected = VerifyCS.Diagnostic(RefParameterAnalyzer.ConvertRefToOutParameterDiagnostic.Rule).WithLocation(0).WithArguments("a");
        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }


    [TestMethod]
    public async Task RefParameterToOut_IsFixable2()
    {
        var test = /* lang=c#-test */"""
            namespace ConsoleApplication1
            {
                class Test
                {
                    void MethodA(ref int a) {
                        int b = a;
                        a = 0;
                    }
                }
            }
            """;


        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    public async Task RefParameterToOut_IsFixable3()
    {
        var test = /* lang=c#-test */"""
            namespace ConsoleApplication1
            {
                class Test
                {
                    void MethodA({|#0:ref|} int a, ref int b) {
                        a = 0;
                        System.Console.WriteLine(a);
                        System.Console.WriteLine(b);
                        b = 0;
                    }
                }
            }
            """;


        var expected = VerifyCS.Diagnostic(RefParameterAnalyzer.ConvertRefToOutParameterDiagnostic.Rule).WithLocation(0).WithArguments("a");
        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }
}
