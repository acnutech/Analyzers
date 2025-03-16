using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyCS = RefParameterAnalyzer.Test.CSharpCodeFixVerifier<
    Acnutech.RefParameterAnalyzer.RefParameterAnalyzerAnalyzer,
    Acnutech.RefParameterAnalyzer.RefParameterAnalyzerCodeFixProvider>;

namespace Acnutech.RefParameterAnalyzer.Test
{
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
            var test = @"
    namespace ConsoleApplication1
    {
        class Test
        {
            void MethodA({|#0:ref|} int a) {}
        }
    }";

            var fixtest = @"
    namespace ConsoleApplication1
    {
        class Test
        {
            void MethodA(int a) {}
        }
    }";

            var expected = VerifyCS.Diagnostic("ACNU0001").WithLocation(0).WithArguments("a");
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
        }
        
        [TestMethod]
        public async Task FixUsagesOfMethodWithRefParameters()
        {
            var test = @"
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

            var fixtest = @"
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

            var expected = VerifyCS.Diagnostic("ACNU0001").WithLocation(0).WithArguments("a");
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
        }

        [TestMethod]
        public async Task FixUsagesOfMethodWithRefParameters2()
        {
            var test = @"
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

            var fixtest = @"
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

            var expected = VerifyCS.Diagnostic("ACNU0001").WithLocation(0).WithArguments("a");
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
        }

        [TestMethod]
        public async Task OverridingMethod_IsOmitted()
        {
            var test = @"
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
            var test = @"
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
            var test = @"
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
            var test = @"
    namespace ConsoleApplication1
    {
        class Test
        {
            void MethodA(ref int a) {
                a = 1;
            }
        }
    }";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task VirtualMethod_IsOmitted()
        {
            var test = @"
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
            var test = @"
        class Test
        {
            void MethodA(ref bool a) {
                System.Threading.Monitor.TryEnter(new object(), ref a);
            }            
        }";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
