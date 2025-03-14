using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyCS = RefParameterAnalyzer.Test.CSharpCodeFixVerifier<
    RefParameterAnalyzer.RefParameterAnalyzerAnalyzer,
    RefParameterAnalyzer.RefParameterAnalyzerCodeFixProvider>;

namespace RefParameterAnalyzer.Test
{
    [TestClass]
    public class RefParameterAnalyzerUnitTest
    {
        //No diagnostics expected to show up
        [TestMethod]
        public async Task EmptyFile_IsIgnored()
        {
            var test = @"";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        //Diagnostic and CodeFix both triggered and checked for
        [TestMethod]
        public async Task RefParameter_IsFixable()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class Test
        {
            void MethodA({|#0:ref|} int a) {}
        }
    }";

            var fixtest = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class Test
        {
            void MethodA(int a) {}
        }
    }";

            var expected = VerifyCS.Diagnostic("RefParameterAnalyzer").WithLocation(0).WithArguments("a");
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
        }

        [TestMethod]
        public async Task OverridingMethod_IsOmitted()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

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
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

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
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

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
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

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
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

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
