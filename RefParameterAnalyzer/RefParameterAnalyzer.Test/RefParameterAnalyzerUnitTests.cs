﻿using System.Threading.Tasks;
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
        public async Task TestMethod1()
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
        class TYPENAME
        {   
        }
    }";

            var expected = VerifyCS.Diagnostic("RefParameterAnalyzer").WithLocation(0).WithArguments("a");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task RequiredRefModifier_NotReported()
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
            void MethodA({|#0:ref|} int a) {
                a = 1;
            }
        }
    }";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
