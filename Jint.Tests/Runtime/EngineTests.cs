﻿using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using Esprima;
using Esprima.Ast;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Debugger;
using Xunit;

namespace Jint.Tests.Runtime
{
    public class EngineTests : IDisposable
    {
        private readonly Engine _engine;
        private int countBreak = 0;
        private StepMode stepMode;

        public EngineTests()
        {
            _engine = new Engine()
                .SetValue("log", new Action<object>(Console.WriteLine))
                .SetValue("assert", new Action<bool>(Assert.True))
                .SetValue("equal", new Action<object, object>(Assert.Equal))
                ;
        }

        void IDisposable.Dispose()
        {
        }


        private void RunTest(string source)
        {
            _engine.Execute(source);
        }

        private string GetEmbeddedFile(string filename)
        {
            const string prefix = "Jint.Tests.Runtime.Scripts.";

            var assembly = typeof(EngineTests).GetTypeInfo().Assembly;
            var scriptPath = prefix + filename;

            using (var stream = assembly.GetManifestResourceStream(scriptPath))
            {
                using (var sr = new StreamReader(stream))
                {
                    return sr.ReadToEnd();
                }
            }
        }

        [Theory]
        [InlineData(42d, "42")]
        [InlineData("Hello", "'Hello'")]
        public void ShouldInterpretLiterals(object expected, string source)
        {
            var engine = new Engine();
            var result = engine.Execute(source).GetCompletionValue().ToObject();

            Assert.Equal(expected, result);
        }

        [Fact]
        public void ShouldInterpretVariableDeclaration()
        {
            var engine = new Engine();
            var result = engine
                .Execute("var foo = 'bar'; foo;")
                .GetCompletionValue()
                .ToObject();

            Assert.Equal("bar", result);
        }

        [Theory]
        [InlineData(4d, "1 + 3")]
        [InlineData(-2d, "1 - 3")]
        [InlineData(3d, "1 * 3")]
        [InlineData(2d, "6 / 3")]
        [InlineData(9d, "15 & 9")]
        [InlineData(15d, "15 | 9")]
        [InlineData(6d, "15 ^ 9")]
        [InlineData(36d, "9 << 2")]
        [InlineData(2d, "9 >> 2")]
        [InlineData(4d, "19 >>> 2")]
        public void ShouldInterpretBinaryExpression(object expected, string source)
        {
            var engine = new Engine();
            var result = engine.Execute(source).GetCompletionValue().ToObject();

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(-59d, "~58")]
        [InlineData(58d, "~~58")]
        public void ShouldInterpretUnaryExpression(object expected, string source)
        {
            var engine = new Engine();
            var result = engine.Execute(source).GetCompletionValue().ToObject();

            Assert.Equal(expected, result);
        }

        [Fact]
        public void ShouldHaveProperReferenceErrorMessage()
        {
            RunTest(@"
                'use strict';
                var arr = [1, 2];
                try {
                    for (i in arr) { }
                    assert(false);
                }
                catch (ex) {
                    assert(ex.message === 'i is not defined');
                }
            ");
        }

        [Fact]
        public void ShouldHaveProperNotAFunctionErrorMessage()
        {
            RunTest(@"
                try {
                    var example = {};
                    example();
                    assert(false);
                }
                catch (ex) {
                    assert(ex.message === 'example is not a function');
                }
            ");
        }

        [Fact]
        public void ShouldEvaluateHasOwnProperty()
        {
            RunTest(@"
                var x = {};
                x.Bar = 42;
                assert(x.hasOwnProperty('Bar'));
            ");
        }

        [Fact]
        public void FunctionConstructorsShouldCreateNewObjects()
        {
            RunTest(@"
                var Vehicle = function () {};
                var vehicle = new Vehicle();
                assert(vehicle != undefined);
            ");
        }

        [Fact]
        public void NewObjectsInheritFunctionConstructorProperties()
        {
            RunTest(@"
                var Vehicle = function () {};
                var vehicle = new Vehicle();
                Vehicle.prototype.wheelCount = 4;
                assert(vehicle.wheelCount == 4);
                assert((new Vehicle()).wheelCount == 4);
            ");
        }

        [Fact]
        public void PrototypeFunctionIsInherited()
        {
            RunTest(@"
                function Body(mass){
                   this.mass = mass;
                }

                Body.prototype.offsetMass = function(dm) {
                   this.mass += dm;
                   return this;
                }

                var b = new Body(36);
                b.offsetMass(6);
                assert(b.mass == 42);
            ");

        }


        [Fact]
        public void FunctionConstructorCall()
        {
            RunTest(@"
                function Body(mass){
                   this.mass = mass;
                }

                var john = new Body(36);
                assert(john.mass == 36);
            ");
        }

        [Fact]
        public void ArrowFunctionCall()
        {
            RunTest(@"
                var add = (a, b) => {
                    return a + b;
                }

                var x = add(1, 2);
                assert(x == 3);
            ");
        }

        [Fact]
        public void ArrowFunctionExpressionCall()
        {
            RunTest(@"
                var add = (a, b) => a + b;

                var x = add(1, 2);
                assert(x === 3);
            ");
        }

        [Fact]
        public void ArrowFunctionScope()
        {
            RunTest(@"
                var bob = {
                    _name: ""Bob"",
                    _friends: [""Alice""],
                    printFriends() {
                        this._friends.forEach(f => assert(this._name === ""Bob"" && f === ""Alice""))
                    }
                };
                bob.printFriends();
            ");
        }

        [Fact]
        public void NewObjectsShouldUsePrivateProperties()
        {
            RunTest(@"
                var Vehicle = function (color) {
                    this.color = color;
                };
                var vehicle = new Vehicle('tan');
                assert(vehicle.color == 'tan');
            ");
        }

        [Fact]
        public void FunctionConstructorsShouldDefinePrototypeChain()
        {
            RunTest(@"
                function Vehicle() {};
                var vehicle = new Vehicle();
                assert(vehicle.hasOwnProperty('constructor') == false);
            ");
        }

        [Fact]
        public void NewObjectsConstructorIsObject()
        {
            RunTest(@"
                var o = new Object();
                assert(o.constructor == Object);
            ");
        }

        [Fact]
        public void NewObjectsIntanceOfConstructorObject()
        {
            RunTest(@"
                var o = new Object();
                assert(o instanceof Object);
            ");
        }

        [Fact]
        public void NewObjectsConstructorShouldBeConstructorObject()
        {
            RunTest(@"
                var Vehicle = function () {};
                var vehicle = new Vehicle();
                assert(vehicle.constructor == Vehicle);
            ");
        }

        [Fact]
        public void NewObjectsIntanceOfConstructorFunction()
        {
            RunTest(@"
                var Vehicle = function () {};
                var vehicle = new Vehicle();
                assert(vehicle instanceof Vehicle);
            ");
        }

        [Fact]
        public void ShouldEvaluateForLoops()
        {
            RunTest(@"
                var foo = 0;
                for (var i = 0; i < 5; i++) {
                    foo += i;
                }
                assert(foo == 10);
            ");
        }

        [Fact]
        public void ShouldEvaluateRecursiveFunctions()
        {
            RunTest(@"
                function fib(n) {
                    if (n < 2) {
                        return n;
                    }
                    return fib(n - 1) + fib(n - 2);
                }
                var result = fib(6);
                assert(result == 8);
            ");
        }

        [Fact]
        public void ShouldAccessObjectProperties()
        {
            RunTest(@"
                var o = {};
                o.Foo = 'bar';
                o.Baz = 42;
                o.Blah = o.Foo + o.Baz;
                assert(o.Blah == 'bar42');
            ");
        }


        [Fact]
        public void ShouldConstructArray()
        {
            RunTest(@"
                var o = [];
                assert(o.length == 0);
            ");
        }

        [Fact]
        public void ArrayPushShouldIncrementLength()
        {
            RunTest(@"
                var o = [];
                o.push(1);
                assert(o.length == 1);
            ");
        }

        [Fact]
        public void ArrayFunctionInitializesLength()
        {
            RunTest(@"
                assert(Array(3).length == 3);
                assert(Array('3').length == 1);
            ");
        }

        [Fact]
        public void ArrayIndexerIsAssigned()
        {
            RunTest(@"
                var n = 8;
                var o = Array(n);
                for (var i = 0; i < n; i++) o[i] = i;
                assert(o[0] == 0);
                assert(o[7] == 7);
            ");
        }

        [Fact]
        public void DenseArrayTurnsToSparseArrayWhenSizeGrowsTooMuch()
        {
            RunTest(@"
                var n = 1024*10+2;
                var o = Array(n);
                for (var i = 0; i < n; i++) o[i] = i;
                assert(o[0] == 0);
                assert(o[n - 1] == n -1);
            ");
        }

        [Fact]
        public void DenseArrayTurnsToSparseArrayWhenSparseIndexed()
        {
            RunTest(@"
                var o = Array();
                o[100] = 1;
                assert(o[100] == 1);
            ");
        }

        [Fact]
        public void ArrayPopShouldDecrementLength()
        {
            RunTest(@"
                var o = [42, 'foo'];
                var pop = o.pop();
                assert(o.length == 1);
                assert(pop == 'foo');
            ");
        }

        [Fact]
        public void ArrayConstructor()
        {
            RunTest(@"
                var o = [];
                assert(o.constructor == Array);
            ");
        }

        [Fact]
        public void DateConstructor()
        {
            RunTest(@"
                var o = new Date();
                assert(o.constructor == Date);
                assert(o.hasOwnProperty('constructor') == false);
            ");
        }

        [Fact]
        public void DateConstructorWithInvalidParameters()
        {
            Assert.Throws<JavaScriptException>(() => RunTest("new Date (1,  Infinity);"));
        }

        [Fact]
        public void ShouldConvertDateToNumber()
        {
            RunTest(@"
                assert(Number(new Date(0)) === 0);
            ");
        }

        [Fact]
        public void DatePrimitiveValueShouldBeNaN()
        {
            RunTest(@"
                assert(isNaN(Date.prototype.valueOf()));
            ");
        }

        [Fact]
        public void MathObjectIsDefined()
        {
            RunTest(@"
                var o = Math.abs(-1)
                assert(o == 1);
            ");
        }

        [Fact]
        public void VoidShouldReturnUndefined()
        {
            RunTest(@"
                assert(void 0 === undefined);
                var x = '1';
                assert(void x === undefined);
                x = 'x';
                assert (isNaN(void x) === true);
                x = new String('-1');
                assert (void x === undefined);
            ");
        }

        [Fact]
        public void TypeofObjectShouldReturnString()
        {
            RunTest(@"
                assert(typeof x === 'undefined');
                assert(typeof 0 === 'number');
                var x = 0;
                assert (typeof x === 'number');
                var x = new Object();
                assert (typeof x === 'object');
            ");
        }

        [Fact]
        public void MathAbsReturnsAbsolute()
        {
            RunTest(@"
                assert(1 == Math.abs(-1));
            ");
        }

        [Fact]
        public void NaNIsNan()
        {
            RunTest(@"
                var x = NaN;
                assert(isNaN(NaN));
                assert(isNaN(Math.abs(x)));
            ");
        }

        [Fact]
        public void ToNumberHandlesStringObject()
        {
            RunTest(@"
                x = new String('1');
                x *= undefined;
                assert(isNaN(x));
            ");
        }

        [Fact]
        public void FunctionScopesAreChained()
        {
            RunTest(@"
                var x = 0;

                function f1(){
                  function f2(){
                    return x;
                  };
                  return f2();

                  var x = 1;
                }

                assert(f1() === undefined);
            ");
        }

        [Fact]
        public void EvalFunctionParseAndExecuteCode()
        {
            RunTest(@"
                var x = 0;
                eval('assert(x == 0)');
            ");
        }

        [Fact]
        public void ForInStatement()
        {
            RunTest(@"
                var x, y, str = '';
                for(var z in this) {
                    str += z;
                }

                equal('xystrz', str);
            ");
        }

        [Fact]
        public void ForInStatementEnumeratesKeys()
        {
            RunTest(@"
                for(var i in 'abc');
				log(i);
                assert(i === '2');
            ");
        }

        [Fact]
        public void WithStatement()
        {
            RunTest(@"
                with (Math) {
                  assert(cos(0) == 1);
                }
            ");
        }

        [Fact]
        public void ObjectExpression()
        {
            RunTest(@"
                var o = { x: 1 };
                assert(o.x == 1);
            ");
        }

        [Fact]
        public void StringFunctionCreatesString()
        {
            RunTest(@"
                assert(String(NaN) === 'NaN');
            ");
        }

        [Fact]
        public void ScopeChainInWithStatement()
        {
            RunTest(@"
                var x = 0;
                var myObj = {x : 'obj'};

                function f1(){
                  var x = 1;
                  function f2(){
                    with(myObj){
                      return x;
                    }
                  };
                  return f2();
                }

                assert(f1() === 'obj');
            ");
        }

        [Fact]
        public void TryCatchBlockStatement()
        {
            RunTest(@"
                var x, y, z;
                try {
                    x = 1;
                    throw new TypeError();
                    x = 2;
                }
                catch(e) {
                    assert(x == 1);
                    assert(e instanceof TypeError);
                    y = 1;
                }
                finally {
                    assert(x == 1);
                    z = 1;
                }

                assert(x == 1);
                assert(y == 1);
                assert(z == 1);
            ");
        }

        [Fact]
        public void FunctionsCanBeAssigned()
        {
            RunTest(@"
                var sin = Math.sin;
                assert(sin(0) == 0);
            ");
        }

        [Fact]
        public void FunctionArgumentsIsDefined()
        {
            RunTest(@"
                function f() {
                    assert(arguments.length > 0);
                }

                f(42);
            ");
        }

        [Fact]
        public void PrimitiveValueFunctions()
        {
            RunTest(@"
                var s = (1).toString();
                assert(s == '1');
            ");
        }

        [Theory]
        [InlineData(true, "'ab' == 'a' + 'b'")]
        public void OperatorsPrecedence(object expected, string source)
        {
            var engine = new Engine();
            var result = engine.Execute(source).GetCompletionValue().ToObject();

            Assert.Equal(expected, result);
        }

        [Fact]
        public void FunctionPrototypeShouldHaveApplyMethod()
        {
            RunTest(@"
                var numbers = [5, 6, 2, 3, 7];
                var max = Math.max.apply(null, numbers);
                assert(max == 7);
            ");
        }

        [Theory]
        [InlineData(double.NaN, "parseInt(NaN)")]
        [InlineData(double.NaN, "parseInt(null)")]
        [InlineData(double.NaN, "parseInt(undefined)")]
        [InlineData(double.NaN, "parseInt(new Boolean(true))")]
        [InlineData(double.NaN, "parseInt(Infinity)")]
        [InlineData(-1d, "parseInt(-1)")]
        [InlineData(-1d, "parseInt('-1')")]
        public void ShouldEvaluateParseInt(object expected, string source)
        {
            var engine = new Engine();
            var result = engine.Execute(source).GetCompletionValue().ToObject();

            Assert.Equal(expected, result);
        }

        [Fact]
        public void ShouldNotExecuteDebuggerStatement()
        {
            new Engine().Execute("debugger");
        }

        [Fact]
        public void ShouldThrowStatementCountOverflow()
        {
            Assert.Throws<StatementsCountOverflowException>(
                () => new Engine(cfg => cfg.MaxStatements(100)).Execute("while(true);")
            );
        }

        [Fact]
        public void ShouldThrowMemoryLimitExceeded()
        {
            Assert.Throws<MemoryLimitExceededException>(
                () => new Engine(cfg => cfg.LimitMemory(2048)).Execute("a=[]; while(true){ a.push(0); }")
            );
        }

        [Fact]
        public void ShouldThrowTimeout()
        {
            Assert.Throws<TimeoutException>(
                () => new Engine(cfg => cfg.TimeoutInterval(new TimeSpan(0, 0, 0, 0, 500))).Execute("while(true);")
            );
        }


        [Fact]
        public void CanDiscardRecursion()
        {
            var script = @"var factorial = function(n) {
                if (n>1) {
                    return n * factorial(n - 1);
                }
            };

            var result = factorial(500);
            ";

            Assert.Throws<RecursionDepthOverflowException>(
                () => new Engine(cfg => cfg.LimitRecursion()).Execute(script)
            );
        }

        [Fact]
        public void ShouldDiscardHiddenRecursion()
        {
            var script = @"var renamedFunc;
            var exec = function(callback) {
                renamedFunc = callback;
                callback();
            };

            var result = exec(function() {
                renamedFunc();
            });
            ";

            Assert.Throws<RecursionDepthOverflowException>(
                () => new Engine(cfg => cfg.LimitRecursion()).Execute(script)
            );
        }

        [Fact]
        public void ShouldRecognizeAndDiscardChainedRecursion()
        {
            var script = @" var funcRoot, funcA, funcB, funcC, funcD;

            var funcRoot = function() {
                funcA();
            };

            var funcA = function() {
                funcB();
            };

            var funcB = function() {
                funcC();
            };

            var funcC = function() {
                funcD();
            };

            var funcD = function() {
                funcRoot();
            };

            funcRoot();
            ";

            Assert.Throws<RecursionDepthOverflowException>(
                () => new Engine(cfg => cfg.LimitRecursion()).Execute(script)
            );
        }

        [Fact]
        public void ShouldProvideCallChainWhenDiscardRecursion()
        {
            var script = @" var funcRoot, funcA, funcB, funcC, funcD;

            var funcRoot = function() {
                funcA();
            };

            var funcA = function() {
                funcB();
            };

            var funcB = function() {
                funcC();
            };

            var funcC = function() {
                funcD();
            };

            var funcD = function() {
                funcRoot();
            };

            funcRoot();
            ";

            RecursionDepthOverflowException exception = null;

            try
            {
                new Engine(cfg => cfg.LimitRecursion()).Execute(script);
            }
            catch (RecursionDepthOverflowException ex)
            {
                exception = ex;
            }

            Assert.NotNull(exception);
            Assert.Equal("funcRoot->funcA->funcB->funcC->funcD", exception.CallChain);
            Assert.Equal("funcRoot", exception.CallExpressionReference);
        }

        [Fact]
        public void ShouldAllowShallowRecursion()
        {
            var script = @"var factorial = function(n) {
                if (n>1) {
                    return n * factorial(n - 1);
                }
            };

            var result = factorial(8);
            ";

            new Engine(cfg => cfg.LimitRecursion(20)).Execute(script);
        }

        [Fact]
        public void ShouldDiscardDeepRecursion()
        {
            var script = @"var factorial = function(n) {
                if (n>1) {
                    return n * factorial(n - 1);
                }
            };

            var result = factorial(38);
            ";

            Assert.Throws<RecursionDepthOverflowException>(
                () => new Engine(cfg => cfg.LimitRecursion(20)).Execute(script)
            );
        }

        [Fact]
        public void ShouldConvertDoubleToStringWithoutLosingPrecision()
        {
            RunTest(@"
                assert(String(14.915832707045631) === '14.915832707045631');
                assert(String(-14.915832707045631) === '-14.915832707045631');
                assert(String(0.5) === '0.5');
                assert(String(0.00000001) === '1e-8');
                assert(String(0.000001) === '0.000001');
                assert(String(-1.0) === '-1');
                assert(String(30.0) === '30');
                assert(String(0.2388906159889881) === '0.2388906159889881');
            ");
        }

        [Fact]
        public void ShouldWriteNumbersUsingBases()
        {
            RunTest(@"
                assert(15.0.toString() === '15');
                assert(15.0.toString(2) === '1111');
                assert(15.0.toString(8) === '17');
                assert(15.0.toString(16) === 'f');
                assert(15.0.toString(17) === 'f');
                assert(15.0.toString(36) === 'f');
                assert(15.1.toString(36) === 'f.3llllllllkau6snqkpygsc3di');
            ");
        }

        [Fact]
        public void ShouldNotAlterSlashesInRegex()
        {
            RunTest(@"
                assert(new RegExp('/').toString() === '///');
            ");
        }

        [Fact]
        public void ShouldHandleEscapedSlashesInRegex()
        {
            RunTest(@"
                var regex = /[a-z]\/[a-z]/;
                assert(regex.test('a/b') === true);
                assert(regex.test('a\\/b') === false);
            ");
        }

        [Fact]
        public void ShouldComputeFractionInBase()
        {
            Assert.Equal("011", _engine.Number.PrototypeObject.ToFractionBase(0.375, 2));
            Assert.Equal("14141414141414141414141414141414141414141414141414", _engine.Number.PrototypeObject.ToFractionBase(0.375, 5));
        }

        [Fact]
        public void ShouldInvokeAFunctionValue()
        {
            RunTest(@"
                function add(x, y) { return x + y; }
            ");

            var add = _engine.GetValue("add");

            Assert.Equal(3, add.Invoke(1, 2));
        }

        [Fact]
        public void ShouldAllowInvokeAFunctionValueWithNullValueAsArgument()
        {
            RunTest(@"
                function get(x) { return x; }
            ");

            var add = _engine.GetValue("get");
            string str = null;
            Assert.Equal(Native.JsValue.Null, add.Invoke(str));
        }


        [Fact]
        public void ShouldNotInvokeNonFunctionValue()
        {
            RunTest(@"
                var x= 10;
            ");

            var x = _engine.GetValue("x");

            Assert.Throws<ArgumentException>(() => x.Invoke(1, 2));
        }

        [Fact]
        public void ShouldInvokeAFunctionValueThatBelongsToAnObject()
        {
            RunTest(@"
                var obj = { foo: 5, getFoo: function (bar) { return 'foo is ' + this.foo + ', bar is ' + bar; } };
            ");

            var obj = _engine.GetValue("obj").AsObject();
            var getFoo = obj.Get("getFoo", obj);

            Assert.Equal("foo is 5, bar is 7", _engine.Invoke(getFoo, obj, new object[] { 7 }).AsString());
        }

        [Fact]
        public void ShouldNotInvokeNonFunctionValueThatBelongsToAnObject()
        {
            RunTest(@"
                var obj = { foo: 2 };
            ");

            var obj = _engine.GetValue("obj").AsObject();
            var foo = obj.Get("foo", obj);

            Assert.Throws<ArgumentException>(() => _engine.Invoke(foo, obj, new object[] { }));
        }

        [Fact]
        public void ShouldNotAllowModifyingSharedUndefinedDescriptor()
        {
            var e = new Engine();
            e.Execute("var x = { literal: true };");

            var pd = e.GetValue("x").AsObject().GetProperty("doesNotExist");
            Assert.Throws<InvalidOperationException>(() => pd.Value = "oh no, assigning this breaks things");
        }

        [Theory]
        [InlineData("0", 0, 16)]
        [InlineData("1", 1, 16)]
        [InlineData("100", 100, 10)]
        [InlineData("1100100", 100, 2)]
        [InlineData("2s", 100, 36)]
        [InlineData("2qgpckvng1s", 10000000000000000L, 36)]
        public void ShouldConvertNumbersToDifferentBase(string expected, long number, int radix)
        {
            var result = _engine.Number.PrototypeObject.ToBase(number, radix);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void JsonParserShouldParseNegativeNumber()
        {
            RunTest(@"
                var a = JSON.parse('{ ""x"":-1 }');
                assert(a.x === -1);

                var b = JSON.parse('{ ""x"": -1 }');
                assert(b.x === -1);
            ");
        }

        [Fact]
        public void JsonParserShouldUseToString()
        {
            RunTest(@"
                var a = JSON.parse(null); // Equivalent to JSON.parse('null')
                assert(a === null);
            ");

            RunTest(@"
                var a = JSON.parse(true); // Equivalent to JSON.parse('true')
                assert(a === true);
            ");

            RunTest(@"
                var a = JSON.parse(false); // Equivalent to JSON.parse('false')
                assert(a === false);
            ");

            RunTest(@"
                try {
                    JSON.parse(undefined); // Equivalent to JSON.parse('undefined')
                    assert(false);
                }
                catch(ex) {
                    assert(ex instanceof SyntaxError);
                }
            ");

            RunTest(@"
                try {
                    JSON.parse({}); // Equivalent to JSON.parse('[object Object]')
                    assert(false);
                }
                catch(ex) {
                    assert(ex instanceof SyntaxError);
                }
            ");

            RunTest(@"
                try {
                    JSON.parse(function() { }); // Equivalent to JSON.parse('function () {}')
                    assert(false);
                }
                catch(ex) {
                    assert(ex instanceof SyntaxError);
                }
            ");
        }

        [Fact]
        public void JsonParserShouldDetectInvalidNegativeNumberSyntax()
        {
            RunTest(@"
                try {
                    JSON.parse('{ ""x"": -.1 }'); // Not allowed
                    assert(false);
                }
                catch(ex) {
                    assert(ex instanceof SyntaxError);
                }
            ");

            RunTest(@"
                try {
                    JSON.parse('{ ""x"": - 1 }'); // Not allowed
                    assert(false);
                }
                catch(ex) {
                    assert(ex instanceof SyntaxError);
                }
            ");
        }

        [Fact]
        public void JsonParserShouldUseReviverFunction()
        {
            RunTest(@"
                var jsonObj = JSON.parse('{""p"": 5}', function (key, value){
                    return typeof value === 'number' ? value * 2 : value;
                });
                assert(jsonObj.p === 10);
            ");

            RunTest(@"
                var expectedKeys = [""1"", ""2"", ""4"", ""6"", ""5"", ""3"", """"];
                var actualKeys = [];
                JSON.parse('{""1"": 1, ""2"": 2, ""3"": {""4"": 4, ""5"": {""6"": 6}}}', function (key, value){
                    actualKeys.push(key);
                    return value;// return the unchanged property value.
                });
                expectedKeys.forEach(function (val, i){
                    assert(actualKeys[i] === val);
                });
            ");
        }

        [Fact]
        [ReplaceCulture("fr-FR")]
        public void ShouldBeCultureInvariant()
        {
            // decimals in french are separated by commas
            var engine = new Engine();

            var result = engine.Execute("1.2 + 2.1").GetCompletionValue().AsNumber();
            Assert.Equal(3.3d, result);

            result = engine.Execute("JSON.parse('{\"x\" : 3.3}').x").GetCompletionValue().AsNumber();
            Assert.Equal(3.3d, result);
        }

        [Fact]
        public void ShouldGetTheLastSyntaxNode()
        {
            var engine = new Engine();
            engine.Execute("1.2");

            var result = engine.GetLastSyntaxNode();
            Assert.Equal(Nodes.Literal, result.Type);
        }

        [Fact]
        public void ShouldGetParseErrorLocation()
        {
            var engine = new Engine();
            try
            {
                engine.Execute("1.2+ new", new ParserOptions(source: "jQuery.js"));
            }
            catch (ParserException e)
            {
                Assert.Equal(1, e.LineNumber);
                Assert.Equal(9, e.Column);
                Assert.Equal("jQuery.js", e.SourceText);
            }
        }
        #region DateParsingAndStrings
        [Fact]
        public void ParseShouldReturnNumber()
        {
            var engine = new Engine();

            var result = engine.Execute("Date.parse('1970-01-01');").GetCompletionValue().AsNumber();
            Assert.Equal(0, result);
        }

        [Fact]
        public void TimeWithinDayShouldHandleNegativeValues()
        {
            RunTest(@"
                // using a date < 1970 so that the primitive value is negative
                var d = new Date(1958, 0, 1);
                d.setMonth(-1);
                assert(d.getDate() == 1);
            ");
        }

        [Fact]
        public void LocalDateTimeShouldNotLoseTimezone()
        {
            var date = new DateTime(2016, 1, 1, 13, 0, 0, DateTimeKind.Local);
            var engine = new Engine().SetValue("localDate", date);
            engine.Execute(@"localDate");
            var actual = engine.GetCompletionValue().AsDate().ToDateTime();
            Assert.Equal(date.ToUniversalTime(), actual.ToUniversalTime());
            Assert.Equal(date.ToLocalTime(), actual.ToLocalTime());
        }

        [Fact]
        public void UtcShouldUseUtc()
        {
            var customTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central Europe Standard Time");

            var engine = new Engine(cfg => cfg.LocalTimeZone(customTimeZone));

            var result = engine.Execute("Date.UTC(1970,0,1)").GetCompletionValue().AsNumber();
            Assert.Equal(0, result);
        }

        [Fact]
        public void ShouldUseLocalTimeZoneOverride()
        {
            const string customName = "Custom Time";
            var customTimeZone = TimeZoneInfo.CreateCustomTimeZone(customName, new TimeSpan(0, 11, 0), customName, customName, customName, null, false);

            var engine = new Engine(cfg => cfg.LocalTimeZone(customTimeZone));

            var epochGetLocalMinutes = engine.Execute("var d = new Date(0); d.getMinutes();").GetCompletionValue().AsNumber();
            Assert.Equal(11, epochGetLocalMinutes);

            var localEpochGetUtcMinutes = engine.Execute("var d = new Date(1970,0,1); d.getUTCMinutes();").GetCompletionValue().AsNumber();
            Assert.Equal(49, localEpochGetUtcMinutes);

            var parseLocalEpoch = engine.Execute("Date.parse('January 1, 1970');").GetCompletionValue().AsNumber();
            Assert.Equal(-11 * 60 * 1000, parseLocalEpoch);

            var epochToLocalString = engine.Execute("var d = new Date(0); d.toString();").GetCompletionValue().AsString();
            Assert.Equal("Thu Jan 01 1970 00:11:00 GMT+00:11", epochToLocalString);

            var epochToUTCString = engine.Execute("var d = new Date(0); d.toUTCString();").GetCompletionValue().AsString();
            Assert.Equal("Thu Jan 01 1970 00:00:00 GMT", epochToUTCString);
        }

        [Theory]
        [InlineData("1970")]
        [InlineData("1970-01")]
        [InlineData("1970-01-01")]
        [InlineData("1970-01-01T00:00")]
        [InlineData("1970-01-01T00:00:00")]
        [InlineData("1970-01-01T00:00:00.000")]
        [InlineData("1970Z")]
        [InlineData("1970-1Z")]
        [InlineData("1970-1-1Z")]
        [InlineData("1970-1-1T0:0Z")]
        [InlineData("1970-1-1T0:0:0Z")]
        [InlineData("1970-1-1T0:0:0.0Z")]
        [InlineData("1970/1Z")]
        [InlineData("1970/1/1Z")]
        [InlineData("1970/1/1 0:0Z")]
        [InlineData("1970/1/1 0:0:0Z")]
        [InlineData("1970/1/1 0:0:0.0Z")]
        [InlineData("January 1, 1970 GMT")]
        [InlineData("1970-01-01T00:00:00.000-00:00")]
        public void ShouldParseAsUtc(string date)
        {
#if NET451
            const string customName = "Custom Time";
            var customTimeZone = TimeZoneInfo.CreateCustomTimeZone(customName, new TimeSpan(7, 11, 0), customName, customName, customName, null, false);
#else
            var customTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Tonga Standard Time");
#endif
            var engine = new Engine(cfg => cfg.LocalTimeZone(customTimeZone));

            engine.SetValue("d", date);
            var result = engine.Execute("Date.parse(d);").GetCompletionValue().AsNumber();

            Assert.Equal(0, result);
        }

        [Theory]
        [InlineData("1970/01")]
        [InlineData("1970/01/01")]
        [InlineData("1970/01/01T00:00")]
        [InlineData("1970/01/01 00:00")]
        [InlineData("1970-1")]
        [InlineData("1970-1-1")]
        [InlineData("1970-1-1T0:0")]
        [InlineData("1970-1-1 0:0")]
        [InlineData("1970/1")]
        [InlineData("1970/1/1")]
        [InlineData("1970/1/1T0:0")]
        [InlineData("1970/1/1 0:0")]
        [InlineData("01-1970")]
        [InlineData("01-01-1970")]
        [InlineData("January 1, 1970")]
        [InlineData("1970-01-01T00:00:00.000+00:11")]
        public void ShouldParseAsLocalTime(string date)
        {
            const int timespanMinutes = 11;
            const int msPriorMidnight = -timespanMinutes * 60 * 1000;
            const string customName = "Custom Time";
            var customTimeZone = TimeZoneInfo.CreateCustomTimeZone(customName, new TimeSpan(0, timespanMinutes, 0), customName, customName, customName, null, false);
            var engine = new Engine(cfg => cfg.LocalTimeZone(customTimeZone)).SetValue("d", date);

            var result = engine.Execute("Date.parse(d);").GetCompletionValue().AsNumber();

            Assert.Equal(msPriorMidnight, result);
        }

        public static System.Collections.Generic.IEnumerable<object[]> TestDates
        {
            get
            {
                yield return new object[] { new DateTime(2000, 1, 1) };
                yield return new object[] { new DateTime(2000, 1, 1, 0, 15, 15, 15) };
                yield return new object[] { new DateTime(2000, 6, 1, 0, 15, 15, 15) };
                yield return new object[] { new DateTime(1900, 1, 1) };
                yield return new object[] { new DateTime(1900, 1, 1, 0, 15, 15, 15) };
                yield return new object[] { new DateTime(1900, 6, 1, 0, 15, 15, 15) };
            }
        }

        [Theory, MemberData("TestDates")]
        public void TestDateToISOStringFormat(DateTime testDate)
        {
            var customTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Tonga Standard Time");

            var engine = new Engine(ctx => ctx.LocalTimeZone(customTimeZone));
            var testDateTimeOffset = new DateTimeOffset(testDate, customTimeZone.GetUtcOffset(testDate));
            engine.Execute(
                string.Format("var d = new Date({0},{1},{2},{3},{4},{5},{6});", testDateTimeOffset.Year, testDateTimeOffset.Month - 1, testDateTimeOffset.Day, testDateTimeOffset.Hour, testDateTimeOffset.Minute, testDateTimeOffset.Second, testDateTimeOffset.Millisecond));
            Assert.Equal(testDateTimeOffset.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture), engine.Execute("d.toISOString();").GetCompletionValue().ToString());
        }

        [Theory, MemberData("TestDates")]
        public void TestDateToStringFormat(DateTime testDate)
        {
            var customTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Tonga Standard Time");

            var engine = new Engine(ctx => ctx.LocalTimeZone(customTimeZone));
            var testDateTimeOffset = new DateTimeOffset(testDate, customTimeZone.GetUtcOffset(testDate));
            engine.Execute(
                string.Format("var d = new Date({0},{1},{2},{3},{4},{5},{6});", testDateTimeOffset.Year, testDateTimeOffset.Month - 1, testDateTimeOffset.Day, testDateTimeOffset.Hour, testDateTimeOffset.Minute, testDateTimeOffset.Second, testDateTimeOffset.Millisecond));

            var expected = testDateTimeOffset.ToString("ddd MMM dd yyyy HH:mm:ss 'GMT'zzz", CultureInfo.InvariantCulture);
            var actual = engine.Execute("d.toString();").GetCompletionValue().ToString();

            Assert.Equal(expected, actual);
        }

        #endregion

        //DateParsingAndStrings
        [Fact]
        public void EmptyStringShouldMatchRegex()
        {
            RunTest(@"
                var regex = /^(?:$)/g;
                assert(''.match(regex) instanceof Array);
            ");
        }

        [Fact]
        public void ShouldExecuteHandlebars()
        {
            var content = GetEmbeddedFile("handlebars.js");

            RunTest(content);

            RunTest(@"
                var source = 'Hello {{name}}';
                var template = Handlebars.compile(source);
                var context = {name: 'Paul'};
                var html = template(context);

                assert('Hello Paul' == html);
            ");
        }

        [Fact]
        public void ShouldExecuteDromaeoBase64()
        {
            RunTest(@"
var startTest = function () { };
var test = function (name, fn) { fn(); };
var endTest = function () { };
var prep = function (fn) { fn(); };
            ");

            var content = GetEmbeddedFile("dromaeo-string-base64.js");
            RunTest(content);
        }

        [Fact]
        public void ShouldExecuteKnockoutWithErrorWhenIntolerant()
        {
            var content = GetEmbeddedFile("knockout-3.4.0.js");

            var ex = Assert.Throws<ParserException>(() => _engine.Execute(content, new ParserOptions { Tolerant = false }));
            Assert.Contains("Duplicate __proto__ fields are not allowed in object literals", ex.Message);
        }

        [Fact]
        public void ShouldExecuteKnockoutWithoutErrorWhenTolerant()
        {
            var content = GetEmbeddedFile("knockout-3.4.0.js");
            _engine.Execute(content, new ParserOptions { Tolerant = true });
        }

        [Fact]
        public void ShouldExecuteLodash()
        {
            var content = GetEmbeddedFile("lodash.min.js");

            RunTest(content);
        }

        [Fact]
        public void DateParseReturnsNaN()
        {
            RunTest(@"
                var d = Date.parse('not a date');
                assert(isNaN(d));
            ");
        }

        [Fact]
        public void ShouldIgnoreHtmlComments()
        {
            RunTest(@"
                var d = Date.parse('not a date'); <!-- a comment -->
                assert(isNaN(d));
            ");
        }

        [Fact]
        public void DateShouldAllowEntireDotNetDateRange()
        {
            var engine = new Engine();

            var minValue = engine.Execute("new Date('0001-01-01T00:00:00.000')").GetCompletionValue().ToObject();
            Assert.Equal(new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Utc), minValue);

            var maxValue = engine.Execute("new Date('9999-12-31T23:59:59.999')").GetCompletionValue().ToObject();
            Assert.Equal(new DateTime(9999, 12, 31, 23, 59, 59, 999, DateTimeKind.Utc), maxValue);
        }

        [Fact]
        public void ShouldConstructNewArrayWithInteger()
        {
            RunTest(@"
                var a = new Array(3);
                assert(a.length === 3);
                assert(a[0] == undefined);
                assert(a[1] == undefined);
                assert(a[2] == undefined);
            ");
        }

        [Fact]
        public void ShouldConstructNewArrayWithString()
        {
            RunTest(@"
                var a = new Array('foo');
                assert(a.length === 1);
                assert(a[0] === 'foo');
            ");
        }

        [Fact]
        public void ShouldThrowRangeExceptionWhenConstructedWithNonInteger()
        {
            RunTest(@"
                var result = false;
                try {
                    var a = new Array(3.4);
                }
                catch(e) {
                    result = e instanceof RangeError;
                }

                assert(result);
            ");
        }

        [Fact]
        public void ShouldInitializeArrayWithSingleIngegerValue()
        {
            RunTest(@"
                var a = [3];
                assert(a.length === 1);
                assert(a[0] === 3);
            ");
        }

        [Fact]
        public void ShouldInitializeJsonObjectArrayWithSingleIntegerValue()
        {
            RunTest(@"
                var x = JSON.parse('{ ""a"": [3] }');
                assert(x.a.length === 1);
                assert(x.a[0] === 3);
            ");
        }

        [Fact]
        public void ShouldInitializeJsonArrayWithSingleIntegerValue()
        {
            RunTest(@"
                var a = JSON.parse('[3]');
                assert(a.length === 1);
                assert(a[0] === 3);
            ");
        }

        [Fact]
        public void ShouldReturnTrueForEmptyIsNaNStatement()
        {
            RunTest(@"
                assert(true === isNaN());
            ");
        }

        [Theory]
        [InlineData(4d, 0, "4")]
        [InlineData(4d, 1, "4.0")]
        [InlineData(4d, 2, "4.00")]
        [InlineData(28.995, 2, "29.00")]
        [InlineData(-28.995, 2, "-29.00")]
        [InlineData(-28.495, 2, "-28.50")]
        [InlineData(-28.445, 2, "-28.45")]
        [InlineData(28.445, 2, "28.45")]
        [InlineData(10.995, 0, "11")]
        public void ShouldRoundToFixedDecimal(double number, int fractionDigits, string result)
        {
            var engine = new Engine();
            var value = engine.Execute(
                String.Format("new Number({0}).toFixed({1})",
                    number.ToString(CultureInfo.InvariantCulture),
                    fractionDigits.ToString(CultureInfo.InvariantCulture)))
                .GetCompletionValue().ToObject();

            Assert.Equal(value, result);
        }



        [Fact]
        public void ShouldSortArrayWhenCompareFunctionReturnsFloatingPointNumber()
        {
            RunTest(@"
                var nums = [1, 1.1, 1.2, 2, 2, 2.1, 2.2];
                nums.sort(function(a,b){return b-a;});
                assert(nums[0] === 2.2);
                assert(nums[1] === 2.1);
                assert(nums[2] === 2);
                assert(nums[3] === 2);
                assert(nums[4] === 1.2);
                assert(nums[5] === 1.1);
                assert(nums[6] === 1);
            ");
        }

        [Fact]
        public void ShouldBreakWhenBreakpointIsReached()
        {
            countBreak = 0;
            stepMode = StepMode.None;

            var engine = new Engine(options => options.DebugMode());

            engine.Break += EngineStep;

            engine.BreakPoints.Add(new BreakPoint(1, 1));

            engine.Execute(@"var local = true;
                if (local === true)
                {}");

            engine.Break -= EngineStep;

            Assert.Equal(1, countBreak);
        }

        [Fact]
        public void ShouldExecuteStepByStep()
        {
            countBreak = 0;
            stepMode = StepMode.Into;

            var engine = new Engine(options => options.DebugMode());

            engine.Step += EngineStep;

            engine.Execute(@"var local = true;
                var creatingSomeOtherLine = 0;
                var lastOneIPromise = true");

            engine.Step -= EngineStep;

            Assert.Equal(3, countBreak);
        }

        [Fact]
        public void ShouldNotBreakTwiceIfSteppingOverBreakpoint()
        {
            countBreak = 0;
            stepMode = StepMode.Into;

            var engine = new Engine(options => options.DebugMode());
            engine.BreakPoints.Add(new BreakPoint(1, 1));
            engine.Step += EngineStep;
            engine.Break += EngineStep;

            engine.Execute(@"var local = true;");

            engine.Step -= EngineStep;
            engine.Break -= EngineStep;

            Assert.Equal(1, countBreak);
        }

        private StepMode EngineStep(object sender, DebugInformation debugInfo)
        {
            Assert.NotNull(sender);
            Assert.IsType(typeof(Engine), sender);
            Assert.NotNull(debugInfo);

            countBreak++;
            return stepMode;
        }

        [Fact]
        public void ShouldShowProperDebugInformation()
        {
            countBreak = 0;
            stepMode = StepMode.None;

            var engine = new Engine(options => options.DebugMode());
            engine.BreakPoints.Add(new BreakPoint(5, 0));
            engine.Break += EngineStepVerifyDebugInfo;

            engine.Execute(@"var global = true;
                            function func1()
                            {
                                var local = false;
;
                            }
                            func1();");

            engine.Break -= EngineStepVerifyDebugInfo;

            Assert.Equal(1, countBreak);
        }

        private StepMode EngineStepVerifyDebugInfo(object sender, DebugInformation debugInfo)
        {
            Assert.NotNull(sender);
            Assert.IsType(typeof(Engine), sender);
            Assert.NotNull(debugInfo);

            Assert.NotNull(debugInfo.CallStack);
            Assert.NotNull(debugInfo.CurrentStatement);
            Assert.NotNull(debugInfo.Locals);

            Assert.Single(debugInfo.CallStack);
//            Assert.Equal("func1()", debugInfo.CallStack.Peek());
            Assert.Contains(debugInfo.Globals, kvp => kvp.Key.Equals("global", StringComparison.Ordinal) && kvp.Value.AsBoolean() == true);
            Assert.Contains(debugInfo.Globals, kvp => kvp.Key.Equals("local", StringComparison.Ordinal) && kvp.Value.AsBoolean() == false);
            Assert.Contains(debugInfo.Locals, kvp => kvp.Key.Equals("local", StringComparison.Ordinal) && kvp.Value.AsBoolean() == false);
            Assert.DoesNotContain(debugInfo.Locals, kvp => kvp.Key.Equals("global", StringComparison.Ordinal));

            countBreak++;
            return stepMode;
        }

        [Fact]
        public void ShouldBreakWhenConditionIsMatched()
        {
            countBreak = 0;
            stepMode = StepMode.None;

            var engine = new Engine(options => options.DebugMode());

            engine.Break += EngineStep;

            engine.BreakPoints.Add(new BreakPoint(5, 16, "condition === true"));
            engine.BreakPoints.Add(new BreakPoint(6, 16, "condition === false"));

            engine.Execute(@"var local = true;
                var condition = true;
                if (local === true)
                {
                ;
                ;
                }");

            engine.Break -= EngineStep;

            Assert.Equal(1, countBreak);
        }

        [Fact]
        public void ShouldNotStepInSameLevelStatementsWhenStepOut()
        {
            countBreak = 0;
            stepMode = StepMode.Out;

            var engine = new Engine(options => options.DebugMode());

            engine.Step += EngineStep;

            engine.Execute(@"function func() // first step - then stepping out
                {
                    ; // shall not step
                    ; // not even here
                }
                func(); // shall not step
                ; // shall not step ");

            engine.Step -= EngineStep;

            Assert.Equal(1, countBreak);
        }

        [Fact]
        public void ShouldNotStepInIfRequiredToStepOut()
        {
            countBreak = 0;

            var engine = new Engine(options => options.DebugMode());

            engine.Step += EngineStepOutWhenInsideFunction;

            engine.Execute(@"function func() // first step
                {
                    ; // third step - now stepping out
                    ; // it should not step here
                }
                func(); // second step
                ; // fourth step ");

            engine.Step -= EngineStepOutWhenInsideFunction;

            Assert.Equal(4, countBreak);
        }

        private StepMode EngineStepOutWhenInsideFunction(object sender, DebugInformation debugInfo)
        {
            Assert.NotNull(sender);
            Assert.IsType(typeof(Engine), sender);
            Assert.NotNull(debugInfo);

            countBreak++;
            if (debugInfo.CallStack.Count > 0)
                return StepMode.Out;

            return StepMode.Into;
        }

        [Fact]
        public void ShouldBreakWhenStatementIsMultiLine()
        {
            countBreak = 0;
            stepMode = StepMode.None;

            var engine = new Engine(options => options.DebugMode());
            engine.BreakPoints.Add(new BreakPoint(4, 33));
            engine.Break += EngineStep;

            engine.Execute(@"var global = true;
                            function func1()
                            {
                                var local =
                                    false;
                            }
                            func1();");

            engine.Break -= EngineStep;

            Assert.Equal(1, countBreak);
        }

        [Fact]
        public void ShouldNotStepInsideIfRequiredToStepOver()
        {
            countBreak = 0;

            var engine = new Engine(options => options.DebugMode());

            stepMode = StepMode.Over;
            engine.Step += EngineStep;

            engine.Execute(@"function func() // first step
                {
                    ; // third step - it shall not step here
                    ; // it shall not step here
                }
                func(); // second step
                ; // third step ");

            engine.Step -= EngineStep;

            Assert.Equal(3, countBreak);
        }

        [Fact]
        public void ShouldStepAllStatementsWithoutInvocationsIfStepOver()
        {
            countBreak = 0;

            var engine = new Engine(options => options.DebugMode());

            stepMode = StepMode.Over;
            engine.Step += EngineStep;

            engine.Execute(@"var step1 = 1; // first step
                var step2 = 2; // second step
                if (step1 !== step2) // third step
                { // fourth step
                    ; // fifth step
                }");

            engine.Step -= EngineStep;

            Assert.Equal(5, countBreak);
        }

        [Fact]
        public void ShouldEvaluateVariableAssignmentFromLeftToRight()
        {
            RunTest(@"
                var keys = ['a']
                  , source = { a: 3}
                  , target = {}
                  , key
                  , i = 0;
                target[key = keys[i++]] = source[key];
                assert(i == 1);
                assert(key == 'a');
                assert(target[key] == 3);
            ");
        }

        [Fact]
        public void ObjectShouldBeExtensible()
        {
            RunTest(@"
                try {
                    Object.defineProperty(Object.defineProperty, 'foo', { value: 1 });
                }
                catch(e) {
                    assert(false);
                }
            ");
        }

        [Fact]
        public void ArrayIndexShouldBeConvertedToUint32()
        {
            // This is missing from ECMA tests suite
            // http://www.ecma-international.org/ecma-262/5.1/#sec-15.4

            RunTest(@"
                var a = [ 'foo' ];
                assert(a[0] === 'foo');
                assert(a['0'] === 'foo');
                assert(a['00'] === undefined);
            ");
        }

        [Fact]
        public void DatePrototypeFunctionWorkOnDateOnly()
        {
            RunTest(@"
                try {
                    var myObj = Object.create(Date.prototype);
                    myObj.toDateString();
                } catch (e) {
                    assert(e instanceof TypeError);
                }
            ");
        }

        [Fact]
        public void DateToStringMethodsShouldUseCurrentTimeZoneAndCulture()
        {
            // Forcing to PDT and FR for tests
            // var PDT = TimeZoneInfo.CreateCustomTimeZone("Pacific Daylight Time", new TimeSpan(-7, 0, 0), "Pacific Daylight Time", "Pacific Daylight Time");
            var PDT = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
            var FR = new CultureInfo("fr-FR");

            var engine = new Engine(options => options.LocalTimeZone(PDT).Culture(FR))
                .SetValue("log", new Action<object>(Console.WriteLine))
                .SetValue("assert", new Action<bool>(Assert.True))
                .SetValue("equal", new Action<object, object>(Assert.Equal))
                ;

            engine.Execute(@"
                    var d = new Date(1433160000000);

                    equal('Mon Jun 01 2015 05:00:00 GMT-07:00', d.toString());
                    equal('Mon Jun 01 2015', d.toDateString());
                    equal('05:00:00 GMT-07:00', d.toTimeString());
                    equal('lundi 1 juin 2015 05:00:00', d.toLocaleString());
                    equal('lundi 1 juin 2015', d.toLocaleDateString());
                    equal('05:00:00', d.toLocaleTimeString());
            ");
        }

        [Fact]
        public void DateShouldHonorTimezoneDaylightSavingRules()
        {
            var EST = TimeZoneInfo.FindSystemTimeZoneById("US Eastern Standard Time");
            var engine = new Engine(options => options.LocalTimeZone(EST))
                .SetValue("log", new Action<object>(Console.WriteLine))
                .SetValue("assert", new Action<bool>(Assert.True))
                .SetValue("equal", new Action<object, object>(Assert.Equal))
                ;

            engine.Execute(@"
                    var d = new Date(2016, 8, 1);

                    equal('Thu Sep 01 2016 00:00:00 GMT-04:00', d.toString());
                    equal('Thu Sep 01 2016', d.toDateString());
            ");
        }

        [Fact]
        public void DateShouldParseToString()
        {
            // Forcing to PDT and FR for tests
            // var PDT = TimeZoneInfo.CreateCustomTimeZone("Pacific Daylight Time", new TimeSpan(-7, 0, 0), "Pacific Daylight Time", "Pacific Daylight Time");
            var PDT = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
            var FR = new CultureInfo("fr-FR");

            new Engine(options => options.LocalTimeZone(PDT).Culture(FR))
                .SetValue("log", new Action<object>(Console.WriteLine))
                .SetValue("assert", new Action<bool>(Assert.True))
                .SetValue("equal", new Action<object, object>(Assert.Equal))
                .Execute(@"
                    var d = new Date(1433160000000);
                    equal(Date.parse(d.toString()), d.valueOf());
                    equal(Date.parse(d.toLocaleString()), d.valueOf());
            ");
        }

        [Fact]
        public void LocaleNumberShouldUseLocalCulture()
        {
            // Forcing to PDT and FR for tests
            // var PDT = TimeZoneInfo.CreateCustomTimeZone("Pacific Daylight Time", new TimeSpan(-7, 0, 0), "Pacific Daylight Time", "Pacific Daylight Time");
            var PDT = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
            var FR = new CultureInfo("fr-FR");

            new Engine(options => options.LocalTimeZone(PDT).Culture(FR))
                .SetValue("log", new Action<object>(Console.WriteLine))
                .SetValue("assert", new Action<bool>(Assert.True))
                .SetValue("equal", new Action<object, object>(Assert.Equal))
                .Execute(@"
                    var d = new Number(-1.23);
                    equal('-1.23', d.toString());
                    equal('-1,23', d.toLocaleString());
            ");
        }

        [Fact]
        public void DateCtorShouldAcceptDate()
        {
            RunTest(@"
                var a = new Date();
                var b = new Date(a);
                assert(String(a) === String(b));
            ");
        }

        [Fact]
        public void RegExpResultIsMutable()
        {
            RunTest(@"
                var match = /quick\s(brown).+?(jumps)/ig.exec('The Quick Brown Fox Jumps Over The Lazy Dog');
                var result = match.shift();
                assert(result === 'Quick Brown Fox Jumps');
            ");
        }

        [Fact]
        public void RegExpSupportsMultiline()
        {
            RunTest(@"
                var rheaders = /^(.*?):[ \t]*([^\r\n]*)$/mg;
                var headersString = 'X-AspNetMvc-Version: 4.0\r\nX-Powered-By: ASP.NET\r\n\r\n';
                match = rheaders.exec(headersString);
                assert('X-AspNetMvc-Version' === match[1]);
                assert('4.0' === match[2]);
            ");

            RunTest(@"
                var rheaders = /^(.*?):[ \t]*(.*?)$/mg;
                var headersString = 'X-AspNetMvc-Version: 4.0\r\nX-Powered-By: ASP.NET\r\n\r\n';
                match = rheaders.exec(headersString);
                assert('X-AspNetMvc-Version' === match[1]);
                assert('4.0' === match[2]);
            ");

            RunTest(@"
                var rheaders = /^(.*?):[ \t]*([^\r\n]*)$/mg;
                var headersString = 'X-AspNetMvc-Version: 4.0\nX-Powered-By: ASP.NET\n\n';
                match = rheaders.exec(headersString);
                assert('X-AspNetMvc-Version' === match[1]);
                assert('4.0' === match[2]);
            ");
        }

        [Fact]
        public void RegExpPrototypeToString()
        {
            RunTest("assert(RegExp.prototype.toString() === '/(?:)/');");
        }

        [Fact]
        public void ShouldSetYearBefore1970()
        {

            RunTest(@"
                var d = new Date('1969-01-01T08:17:00');
                d.setYear(2015);
                equal('2015-01-01T08:17:00.000Z', d.toISOString());
            ");
        }

        [Fact]
        public void ShouldUseReplaceMarkers()
        {
            RunTest(@"
                var re = /a/g;
                var str = 'abab';
                var newstr = str.replace(re, '$\'x');
                equal('babxbbxb', newstr);
            ");
        }

        [Fact]
        public void ExceptionShouldHaveLocationOfInnerFunction()
        {
            try
            {
                new Engine()
                    .Execute(@"
                    function test(s) {
                        o.boom();
                    }
                    test('arg');
                ");
            }
            catch (JavaScriptException ex)
            {
                Assert.Equal(3, ex.LineNumber);
            }
        }

        [Fact]
        public void GlobalRegexLiteralShouldNotKeepState()
        {
            RunTest(@"
				var url = 'https://www.example.com';

				assert(isAbsolutePath(url));
				assert(isAbsolutePath(url));
				assert(isAbsolutePath(url));

				function isAbsolutePath(path) {
					return /\.+/g.test(path);
				}
            ");
        }

        [Fact]
        public void ShouldCompareInnerValueOfClrInstances()
        {
            var engine = new Engine();

            // Create two separate Guid with identical inner values.
            var guid1 = Guid.NewGuid();
            var guid2 = new Guid(guid1.ToString());

            engine.SetValue("guid1", guid1);
            engine.SetValue("guid2", guid2);

            var result = engine.Execute("guid1 == guid2").GetCompletionValue().AsBoolean();

            Assert.True(result);
        }

        [Fact]
        public void CanStringifyToConsole()
        {
            var engine = new Engine(options => options.AllowClr(typeof(Console).Assembly));
            engine.Execute("System.Console.WriteLine(JSON.stringify({x:12, y:14}));");
        }

        [Fact]
        public void ShouldNotCompareClrInstancesWithObjects()
        {
            var engine = new Engine();

            var guid1 = Guid.NewGuid();

            engine.SetValue("guid1", guid1);

            var result = engine.Execute("guid1 == {}").GetCompletionValue().AsBoolean();

            Assert.False(result);
        }

        [Fact]
        public void ShouldStringifyNumWithoutV8DToA()
        {
            // 53.6841659 cannot be converted by V8's DToA => "old" DToA code will be used.
            var engine = new Engine();
            var val = engine.Execute("JSON.stringify(53.6841659)").GetCompletionValue();

            Assert.Equal("53.6841659", val.AsString());
        }

        [Fact]
        public void ShouldStringifyObjectWithPropertiesToSameRef()
        {
            var engine = new Engine();
            var res = engine.Execute(@"
                var obj = {
                    a : [],
                    a1 : ['str'],
                    a2 : {},
                    a3 : { 'prop' : 'val' }
                };
                obj.b = obj.a;
                obj.b1 = obj.a1;
                JSON.stringify(obj);
            ").GetCompletionValue();

            Assert.True(res == "{\"a\":[],\"a1\":[\"str\"],\"a2\":{},\"a3\":{\"prop\":\"val\"},\"b\":[],\"b1\":[\"str\"]}");
        }

        [Fact]
        public void ShouldThrowOnSerializingCyclicRefObject()
        {
            var engine = new Engine();
            var res = engine.Execute(@"
                (function(){
                    try{
                        a = [];
                        a[0] = a;
                        my_text = JSON.stringify(a);
                    }
                    catch(ex){
                        return ex.message;
                    }
                })();
            ").GetCompletionValue();

            Assert.True(res == "Cyclic reference detected.");
        }

        [Theory]
        [InlineData("", "escape('')")]
        [InlineData("%u0100%u0101%u0102", "escape('\u0100\u0101\u0102')")]
        [InlineData("%uFFFD%uFFFE%uFFFF", "escape('\ufffd\ufffe\uffff')")]
        [InlineData("%uD834%uDF06", "escape('\ud834\udf06')")]
        [InlineData("%00%01%02%03", "escape('\x00\x01\x02\x03')")]
        [InlineData("%2C", "escape(',')")]
        [InlineData("%3A%3B%3C%3D%3E%3F", "escape(':;<=>?')")]
        [InlineData("%60", "escape('`')")]
        [InlineData("%7B%7C%7D%7E%7F%80", "escape('{|}~\x7f\x80')")]
        [InlineData("%FD%FE%FF", "escape('\xfd\xfe\xff')")]
        [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789@*_+-./", "escape('ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789@*_+-./')")]
        public void ShouldEvaluateEscape(object expected, string source)
        {
            var engine = new Engine();
            var result = engine.Execute(source).GetCompletionValue().ToObject();

            Assert.Equal(expected, result);
        }

        [Theory]
        //https://github.com/tc39/test262/blob/master/test/annexB/built-ins/unescape/empty-string.js
        [InlineData("", "unescape('')")]
        //https://github.com/tc39/test262/blob/master/test/annexB/built-ins/unescape/four-ignore-bad-u.js
        [InlineData("%U0000", "unescape('%U0000')")]
        [InlineData("%t0000", "unescape('%t0000')")]
        [InlineData("%v0000", "unescape('%v0000')")]
        [InlineData("%" + "\x00" + "00", "unescape('%%0000')")]
        //https://github.com/tc39/test262/blob/master/test/annexB/built-ins/unescape/four-ignore-end-str.js
        [InlineData("%u", "unescape('%u')")]
        [InlineData("%u0", "unescape('%u0')")]
        [InlineData("%u1", "unescape('%u1')")]
        [InlineData("%u2", "unescape('%u2')")]
        [InlineData("%u3", "unescape('%u3')")]
        [InlineData("%u4", "unescape('%u4')")]
        [InlineData("%u5", "unescape('%u5')")]
        [InlineData("%u6", "unescape('%u6')")]
        [InlineData("%u7", "unescape('%u7')")]
        [InlineData("%u8", "unescape('%u8')")]
        [InlineData("%u9", "unescape('%u9')")]
        [InlineData("%ua", "unescape('%ua')")]
        [InlineData("%uA", "unescape('%uA')")]
        [InlineData("%ub", "unescape('%ub')")]
        [InlineData("%uB", "unescape('%uB')")]
        [InlineData("%uc", "unescape('%uc')")]
        [InlineData("%uC", "unescape('%uC')")]
        [InlineData("%ud", "unescape('%ud')")]
        [InlineData("%uD", "unescape('%uD')")]
        [InlineData("%ue", "unescape('%ue')")]
        [InlineData("%uE", "unescape('%uE')")]
        [InlineData("%uf", "unescape('%uf')")]
        [InlineData("%uF", "unescape('%uF')")]
        [InlineData("%u01", "unescape('%u01')")]
        [InlineData("%u02", "unescape('%u02')")]
        [InlineData("%u03", "unescape('%u03')")]
        [InlineData("%u04", "unescape('%u04')")]
        [InlineData("%u05", "unescape('%u05')")]
        [InlineData("%u06", "unescape('%u06')")]
        [InlineData("%u07", "unescape('%u07')")]
        [InlineData("%u08", "unescape('%u08')")]
        [InlineData("%u09", "unescape('%u09')")]
        [InlineData("%u0a", "unescape('%u0a')")]
        [InlineData("%u0A", "unescape('%u0A')")]
        [InlineData("%u0b", "unescape('%u0b')")]
        [InlineData("%u0B", "unescape('%u0B')")]
        [InlineData("%u0c", "unescape('%u0c')")]
        [InlineData("%u0C", "unescape('%u0C')")]
        [InlineData("%u0d", "unescape('%u0d')")]
        [InlineData("%u0D", "unescape('%u0D')")]
        [InlineData("%u0e", "unescape('%u0e')")]
        [InlineData("%u0E", "unescape('%u0E')")]
        [InlineData("%u0f", "unescape('%u0f')")]
        [InlineData("%u0F", "unescape('%u0F')")]
        [InlineData("%u000", "unescape('%u000')")]
        [InlineData("%u001", "unescape('%u001')")]
        [InlineData("%u002", "unescape('%u002')")]
        [InlineData("%u003", "unescape('%u003')")]
        [InlineData("%u004", "unescape('%u004')")]
        [InlineData("%u005", "unescape('%u005')")]
        [InlineData("%u006", "unescape('%u006')")]
        [InlineData("%u007", "unescape('%u007')")]
        [InlineData("%u008", "unescape('%u008')")]
        [InlineData("%u009", "unescape('%u009')")]
        [InlineData("%u00a", "unescape('%u00a')")]
        [InlineData("%u00A", "unescape('%u00A')")]
        [InlineData("%u00b", "unescape('%u00b')")]
        [InlineData("%u00B", "unescape('%u00B')")]
        [InlineData("%u00c", "unescape('%u00c')")]
        [InlineData("%u00C", "unescape('%u00C')")]
        [InlineData("%u00d", "unescape('%u00d')")]
        [InlineData("%u00D", "unescape('%u00D')")]
        [InlineData("%u00e", "unescape('%u00e')")]
        [InlineData("%u00E", "unescape('%u00E')")]
        [InlineData("%u00f", "unescape('%u00f')")]
        [InlineData("%u00F", "unescape('%u00F')")]
        //https://github.com/tc39/test262/blob/master/test/annexB/built-ins/unescape/four-ignore-non-hex.js
        [InlineData("%u000%0", "unescape('%u000%0')")]
        [InlineData("%u000g0", "unescape('%u000g0')")]
        [InlineData("%u000G0", "unescape('%u000G0')")]
        [InlineData("%u00g00", "unescape('%u00g00')")]
        [InlineData("%u00G00", "unescape('%u00G00')")]
        [InlineData("%u0g000", "unescape('%u0g000')")]
        [InlineData("%u0G000", "unescape('%u0G000')")]
        [InlineData("%ug0000", "unescape('%ug0000')")]
        [InlineData("%uG0000", "unescape('%uG0000')")]
        [InlineData("%u000u0", "unescape('%u000u0')")]
        [InlineData("%u000U0", "unescape('%u000U0')")]
        [InlineData("%u00u00", "unescape('%u00u00')")]
        [InlineData("%u00U00", "unescape('%u00U00')")]
        [InlineData("%u0u000", "unescape('%u0u000')")]
        [InlineData("%u0U000", "unescape('%u0U000')")]
        [InlineData("%uu0000", "unescape('%uu0000')")]
        [InlineData("%uU0000", "unescape('%uU0000')")]
        //https://github.com/tc39/test262/blob/master/test/annexB/built-ins/unescape/four.js
        [InlineData("%0" + "\x00" + "0", "unescape('%0%u00000')")]
        [InlineData("%0" + "\x01" + "0", "unescape('%0%u00010')")]
        [InlineData("%0)0", "unescape('%0%u00290')")]
        [InlineData("%0*0", "unescape('%0%u002a0')")]
        [InlineData("%0*0", "unescape('%0%u002A0')")]
        [InlineData("%0+0", "unescape('%0%u002b0')")]
        [InlineData("%0+0", "unescape('%0%u002B0')")]
        [InlineData("%0,0", "unescape('%0%u002c0')")]
        [InlineData("%0,0", "unescape('%0%u002C0')")]
        [InlineData("%0-0", "unescape('%0%u002d0')")]
        [InlineData("%0-0", "unescape('%0%u002D0')")]
        [InlineData("%090", "unescape('%0%u00390')")]
        [InlineData("%0:0", "unescape('%0%u003a0')")]
        [InlineData("%0:0", "unescape('%0%u003A0')")]
        [InlineData("%0?0", "unescape('%0%u003f0')")]
        [InlineData("%0?0", "unescape('%0%u003F0')")]
        [InlineData("%0@0", "unescape('%0%u00400')")]
        [InlineData("%0Z0", "unescape('%0%u005a0')")]
        [InlineData("%0Z0", "unescape('%0%u005A0')")]
        [InlineData("%0[0", "unescape('%0%u005b0')")]
        [InlineData("%0[0", "unescape('%0%u005B0')")]
        [InlineData("%0^0", "unescape('%0%u005e0')")]
        [InlineData("%0^0", "unescape('%0%u005E0')")]
        [InlineData("%0_0", "unescape('%0%u005f0')")]
        [InlineData("%0_0", "unescape('%0%u005F0')")]
        [InlineData("%0`0", "unescape('%0%u00600')")]
        [InlineData("%0a0", "unescape('%0%u00610')")]
        [InlineData("%0z0", "unescape('%0%u007a0')")]
        [InlineData("%0z0", "unescape('%0%u007A0')")]
        [InlineData("%0{0", "unescape('%0%u007b0')")]
        [InlineData("%0{0", "unescape('%0%u007B0')")]
        [InlineData("%0" + "\ufffe" + "0", "unescape('%0%ufffe0')")]
        [InlineData("%0" + "\ufffe" + "0", "unescape('%0%uFffe0')")]
        [InlineData("%0" + "\ufffe" + "0", "unescape('%0%ufFfe0')")]
        [InlineData("%0" + "\ufffe" + "0", "unescape('%0%uffFe0')")]
        [InlineData("%0" + "\ufffe" + "0", "unescape('%0%ufffE0')")]
        [InlineData("%0" + "\ufffe" + "0", "unescape('%0%uFFFE0')")]
        [InlineData("%0" + "\uffff" + "0", "unescape('%0%uffff0')")]
        [InlineData("%0" + "\uffff" + "0", "unescape('%0%uFfff0')")]
        [InlineData("%0" + "\uffff" + "0", "unescape('%0%ufFff0')")]
        [InlineData("%0" + "\uffff" + "0", "unescape('%0%uffFf0')")]
        [InlineData("%0" + "\uffff" + "0", "unescape('%0%ufffF0')")]
        [InlineData("%0" + "\uffff" + "0", "unescape('%0%uFFFF0')")]
        //https://github.com/tc39/test262/blob/master/test/annexB/built-ins/unescape/two-ignore-end-str.js
        [InlineData("%", "unescape('%')")]
        [InlineData("%0", "unescape('%0')")]
        [InlineData("%1", "unescape('%1')")]
        [InlineData("%2", "unescape('%2')")]
        [InlineData("%3", "unescape('%3')")]
        [InlineData("%4", "unescape('%4')")]
        [InlineData("%5", "unescape('%5')")]
        [InlineData("%6", "unescape('%6')")]
        [InlineData("%7", "unescape('%7')")]
        [InlineData("%8", "unescape('%8')")]
        [InlineData("%9", "unescape('%9')")]
        [InlineData("%a", "unescape('%a')")]
        [InlineData("%A", "unescape('%A')")]
        [InlineData("%b", "unescape('%b')")]
        [InlineData("%B", "unescape('%B')")]
        [InlineData("%c", "unescape('%c')")]
        [InlineData("%C", "unescape('%C')")]
        [InlineData("%d", "unescape('%d')")]
        [InlineData("%D", "unescape('%D')")]
        [InlineData("%e", "unescape('%e')")]
        [InlineData("%E", "unescape('%E')")]
        [InlineData("%f", "unescape('%f')")]
        [InlineData("%F", "unescape('%F')")]
        //https://github.com/tc39/test262/blob/master/test/annexB/built-ins/unescape/two-ignore-non-hex.js
        [InlineData("%0%0", "unescape('%0%0')")]
        [InlineData("%0g0", "unescape('%0g0')")]
        [InlineData("%0G0", "unescape('%0G0')")]
        [InlineData("%g00", "unescape('%g00')")]
        [InlineData("%G00", "unescape('%G00')")]
        [InlineData("%0u0", "unescape('%0u0')")]
        [InlineData("%0U0", "unescape('%0U0')")]
        [InlineData("%u00", "unescape('%u00')")]
        [InlineData("%U00", "unescape('%U00')")]
        //https://github.com/tc39/test262/blob/master/test/annexB/built-ins/unescape/two.js
        [InlineData("%0" + "\x00" + "00", "unescape('%0%0000')")]
        [InlineData("%0" + "\x01" + "00", "unescape('%0%0100')")]
        [InlineData("%0)00", "unescape('%0%2900')")]
        [InlineData("%0*00", "unescape('%0%2a00')")]
        [InlineData("%0*00", "unescape('%0%2A00')")]
        [InlineData("%0+00", "unescape('%0%2b00')")]
        [InlineData("%0+00", "unescape('%0%2B00')")]
        [InlineData("%0,00", "unescape('%0%2c00')")]
        [InlineData("%0,00", "unescape('%0%2C00')")]
        [InlineData("%0-00", "unescape('%0%2d00')")]
        [InlineData("%0-00", "unescape('%0%2D00')")]
        [InlineData("%0900", "unescape('%0%3900')")]
        [InlineData("%0:00", "unescape('%0%3a00')")]
        [InlineData("%0:00", "unescape('%0%3A00')")]
        [InlineData("%0?00", "unescape('%0%3f00')")]
        [InlineData("%0?00", "unescape('%0%3F00')")]
        [InlineData("%0@00", "unescape('%0%4000')")]
        [InlineData("%0Z00", "unescape('%0%5a00')")]
        [InlineData("%0Z00", "unescape('%0%5A00')")]
        [InlineData("%0[00", "unescape('%0%5b00')")]
        [InlineData("%0[00", "unescape('%0%5B00')")]
        [InlineData("%0^00", "unescape('%0%5e00')")]
        [InlineData("%0^00", "unescape('%0%5E00')")]
        [InlineData("%0_00", "unescape('%0%5f00')")]
        [InlineData("%0_00", "unescape('%0%5F00')")]
        [InlineData("%0`00", "unescape('%0%6000')")]
        [InlineData("%0a00", "unescape('%0%6100')")]
        [InlineData("%0z00", "unescape('%0%7a00')")]
        [InlineData("%0z00", "unescape('%0%7A00')")]
        [InlineData("%0{00", "unescape('%0%7b00')")]
        [InlineData("%0{00", "unescape('%0%7B00')")]
        public void ShouldEvaluateUnescape(object expected, string source)
        {
            var engine = new Engine();
            var result = engine.Execute(source).GetCompletionValue().ToObject();

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("new Date(1969,0,1,19,45,30,500).getHours()", 19)]
        [InlineData("new Date(1970,0,1,19,45,30,500).getHours()", 19)]
        [InlineData("new Date(1971,0,1,19,45,30,500).getHours()", 19)]
        [InlineData("new Date(1969,0,1,19,45,30,500).getMinutes()", 45)]
        [InlineData("new Date(1970,0,1,19,45,30,500).getMinutes()", 45)]
        [InlineData("new Date(1971,0,1,19,45,30,500).getMinutes()", 45)]
        [InlineData("new Date(1969,0,1,19,45,30,500).getSeconds()", 30)]
        [InlineData("new Date(1970,0,1,19,45,30,500).getSeconds()", 30)]
        [InlineData("new Date(1971,0,1,19,45,30,500).getSeconds()", 30)]
        //[InlineData("new Date(1969,0,1,19,45,30,500).getMilliseconds()", 500)]
        //[InlineData("new Date(1970,0,1,19,45,30,500).getMilliseconds()", 500)]
        //[InlineData("new Date(1971,0,1,19,45,30,500).getMilliseconds()", 500)]
        public void ShouldExtractDateParts(string source, double expected)
        {
            var engine = new Engine();
            var result = engine.Execute(source).GetCompletionValue().ToObject();

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("'abc'.padStart(10)", "       abc")]
        [InlineData("'abc'.padStart(10, \"foo\")", "foofoofabc")]
        [InlineData("'abc'.padStart(6, \"123456\")", "123abc")]
        [InlineData("'abc'.padStart(8, \"0\")", "00000abc")]
        [InlineData("'abc'.padStart(1)", "abc")]
        public void ShouldPadStart(string source, object expected)
        {
            var engine = new Engine();
            var result = engine.Execute(source).GetCompletionValue().ToObject();

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("'abc'.padEnd(10)", "abc       ")]
        [InlineData("'abc'.padEnd(10, \"foo\")", "abcfoofoof")]
        [InlineData("'abc'.padEnd(6, \"123456\")", "abc123")]
        [InlineData("'abc'.padEnd(1)", "abc")]
        public void ShouldPadEnd(string source, object expected)
        {
            var engine = new Engine();
            var result = engine.Execute(source).GetCompletionValue().ToObject();

            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests for startsWith - tests created from MDN and https://github.com/mathiasbynens/String.prototype.startsWith/blob/master/tests/tests.js
        /// </summary>
        [Theory]
        [InlineData("'To be, or not to be, that is the question.'.startsWith('To be')", true)]
        [InlineData("'To be, or not to be, that is the question.'.startsWith('not to be')", false)]
        [InlineData("'To be, or not to be, that is the question.'.startsWith()", false)]
        [InlineData("'To be, or not to be, that is the question.'.startsWith('not to be', 10)", true)]
        [InlineData("'undefined'.startsWith()", true)]
        [InlineData("'undefined'.startsWith(undefined)", true)]
        [InlineData("'undefined'.startsWith(null)", false)]
        [InlineData("'null'.startsWith()", false)]
        [InlineData("'null'.startsWith(undefined)", false)]
        [InlineData("'null'.startsWith(null)", true)]
        [InlineData("'abc'.startsWith()", false)]
        [InlineData("'abc'.startsWith('')", true)]
        [InlineData("'abc'.startsWith('\0')", false)]
        [InlineData("'abc'.startsWith('a')", true)]
        [InlineData("'abc'.startsWith('b')", false)]
        [InlineData("'abc'.startsWith('ab')", true)]
        [InlineData("'abc'.startsWith('bc')", false)]
        [InlineData("'abc'.startsWith('abc')", true)]
        [InlineData("'abc'.startsWith('bcd')", false)]
        [InlineData("'abc'.startsWith('abcd')", false)]
        [InlineData("'abc'.startsWith('bcde')", false)]
        [InlineData("'abc'.startsWith('', 1)", true)]
        [InlineData("'abc'.startsWith('\0', 1)", false)]
        [InlineData("'abc'.startsWith('a', 1)", false)]
        [InlineData("'abc'.startsWith('b', 1)", true)]
        [InlineData("'abc'.startsWith('ab', 1)", false)]
        [InlineData("'abc'.startsWith('bc', 1)", true)]
        [InlineData("'abc'.startsWith('abc', 1)", false)]
        [InlineData("'abc'.startsWith('bcd', 1)", false)]
        [InlineData("'abc'.startsWith('abcd', 1)", false)]
        [InlineData("'abc'.startsWith('bcde', 1)", false)]
        public void ShouldStartWith(string source, object expected)
        {
            var engine = new Engine();
            var result = engine.Execute(source).GetCompletionValue().ToObject();

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("throw {}", "undefined")]
        [InlineData("throw {message:null}", "null")]
        [InlineData("throw {message:''}", "")]
        [InlineData("throw {message:2}", "2")]
        public void ShouldAllowNonStringMessage(string source, string expected)
        {
            var engine = new Engine();
            var ex = Assert.Throws<JavaScriptException>(() => engine.Execute(source));
            Assert.Equal(expected, ex.Message);
        }

        [Theory]
        //Months
        [InlineData("new Date(2017, 0, 1, 0, 0, 0)", "new Date(2016, 12, 1, 0, 0, 0)")]
        [InlineData("new Date(2016, 0, 1, 23, 59, 59)", "new Date(2015, 12, 1, 23, 59, 59)")]
        [InlineData("new Date(2013, 0, 1, 0, 0, 0)", "new Date(2012, 12, 1, 0, 0, 0)")]
        [InlineData("new Date(2013, 0, 29, 23, 59, 59)", "new Date(2012, 12, 29, 23, 59, 59)")]
        [InlineData("new Date(2015, 11, 1, 0, 0, 0)", "new Date(2016, -1, 1, 0, 0, 0)")]
        [InlineData("new Date(2014, 11, 1, 23, 59, 59)", "new Date(2015, -1, 1, 23, 59, 59)")]
        [InlineData("new Date(2011, 11, 1, 0, 0, 0)", "new Date(2012, -1, 1, 0, 0, 0)")]
        [InlineData("new Date(2011, 11, 29, 23, 59, 59)", "new Date(2012, -1, 29, 23, 59, 59)")]
        [InlineData("new Date(2015, 1, 1, 0, 0, 0)", "new Date(2016, -11, 1, 0, 0, 0)")]
        [InlineData("new Date(2014, 1, 1, 23, 59, 59)", "new Date(2015, -11, 1, 23, 59, 59)")]
        [InlineData("new Date(2011, 1, 1, 0, 0, 0)", "new Date(2012, -11, 1, 0, 0, 0)")]
        [InlineData("new Date(2011, 2, 1, 23, 59, 59)", "new Date(2012, -11, 29, 23, 59, 59)")]
        [InlineData("new Date(2015, 0, 1, 0, 0, 0)", "new Date(2016, -12, 1, 0, 0, 0)")]
        [InlineData("new Date(2014, 0, 1, 23, 59, 59)", "new Date(2015, -12, 1, 23, 59, 59)")]
        [InlineData("new Date(2011, 0, 1, 0, 0, 0)", "new Date(2012, -12, 1, 0, 0, 0)")]
        [InlineData("new Date(2011, 0, 29, 23, 59, 59)", "new Date(2012, -12, 29, 23, 59, 59)")]
        [InlineData("new Date(2014, 11, 1, 0, 0, 0)", "new Date(2016, -13, 1, 0, 0, 0)")]
        [InlineData("new Date(2013, 11, 1, 23, 59, 59)", "new Date(2015, -13, 1, 23, 59, 59)")]
        [InlineData("new Date(2010, 11, 1, 0, 0, 0)", "new Date(2012, -13, 1, 0, 0, 0)")]
        [InlineData("new Date(2010, 11, 29, 23, 59, 59)", "new Date(2012, -13, 29, 23, 59, 59)")]
        [InlineData("new Date(2013, 11, 1, 0, 0, 0)", "new Date(2016, -25, 1, 0, 0, 0)")]
        [InlineData("new Date(2012, 11, 1, 23, 59, 59)", "new Date(2015, -25, 1, 23, 59, 59)")]
        [InlineData("new Date(2009, 11, 1, 0, 0, 0)", "new Date(2012, -25, 1, 0, 0, 0)")]
        [InlineData("new Date(2009, 11, 29, 23, 59, 59)", "new Date(2012, -25, 29, 23, 59, 59)")]
        //Days
        [InlineData("new Date(2016, 1, 11, 0, 0, 0)", "new Date(2016, 0, 42, 0, 0, 0)")]
        [InlineData("new Date(2016, 0, 11, 23, 59, 59)", "new Date(2015, 11, 42, 23, 59, 59)")]
        [InlineData("new Date(2012, 3, 11, 0, 0, 0)", "new Date(2012, 2, 42, 0, 0, 0)")]
        [InlineData("new Date(2012, 2, 13, 23, 59, 59)", "new Date(2012, 1, 42, 23, 59, 59)")]
        [InlineData("new Date(2015, 11, 31, 0, 0, 0)", "new Date(2016, 0, 0, 0, 0, 0)")]
        [InlineData("new Date(2015, 10, 30, 23, 59, 59)", "new Date(2015, 11, 0, 23, 59, 59)")]
        [InlineData("new Date(2012, 1, 29, 0, 0, 0)", "new Date(2012, 2, 0, 0, 0, 0)")]
        [InlineData("new Date(2012, 0, 31, 23, 59, 59)", "new Date(2012, 1, 0, 23, 59, 59)")]
        [InlineData("new Date(2015, 10, 24, 0, 0, 0)", "new Date(2016, 0, -37, 0, 0, 0)")]
        [InlineData("new Date(2015, 9, 24, 23, 59, 59)", "new Date(2015, 11, -37, 23, 59, 59)")]
        [InlineData("new Date(2012, 0, 23, 0, 0, 0)", "new Date(2012, 2, -37, 0, 0, 0)")]
        [InlineData("new Date(2011, 11, 25, 23, 59, 59)", "new Date(2012, 1, -37, 23, 59, 59)")]
        //Hours
        [InlineData("new Date(2016, 0, 2, 1, 0, 0)", "new Date(2016, 0, 1, 25, 0, 0)")]
        [InlineData("new Date(2015, 11, 2, 1, 59, 59)", "new Date(2015, 11, 1, 25, 59, 59)")]
        [InlineData("new Date(2012, 2, 2, 1, 0, 0)", "new Date(2012, 2, 1, 25, 0, 0)")]
        [InlineData("new Date(2012, 2, 1, 1, 59, 59)", "new Date(2012, 1, 29, 25, 59, 59)")]
        [InlineData("new Date(2016, 0, 19, 3, 0, 0)", "new Date(2016, 0, 1, 435, 0, 0)")]
        [InlineData("new Date(2015, 11, 19, 3, 59, 59)", "new Date(2015, 11, 1, 435, 59, 59)")]
        [InlineData("new Date(2012, 2, 19, 3, 0, 0)", "new Date(2012, 2, 1, 435, 0, 0)")]
        [InlineData("new Date(2012, 2, 18, 3, 59, 59)", "new Date(2012, 1, 29, 435, 59, 59)")]
        [InlineData("new Date(2015, 11, 31, 23, 0, 0)", "new Date(2016, 0, 1, -1, 0, 0)")]
        [InlineData("new Date(2015, 10, 30, 23, 59, 59)", "new Date(2015, 11, 1, -1, 59, 59)")]
        [InlineData("new Date(2012, 1, 29, 23, 0, 0)", "new Date(2012, 2, 1, -1, 0, 0)")]
        [InlineData("new Date(2012, 1, 28, 23, 59, 59)", "new Date(2012, 1, 29, -1, 59, 59)")]
        [InlineData("new Date(2015, 11, 3, 18, 0, 0)", "new Date(2016, 0, 1, -678, 0, 0)")]
        [InlineData("new Date(2015, 10, 2, 18, 59, 59)", "new Date(2015, 11, 1, -678, 59, 59)")]
        [InlineData("new Date(2012, 1, 1, 18, 0, 0)", "new Date(2012, 2, 1, -678, 0, 0)")]
        [InlineData("new Date(2012, 0, 31, 18, 59, 59)", "new Date(2012, 1, 29, -678, 59, 59)")]
        // Minutes
        [InlineData("new Date(2016, 0, 1, 1, 0, 0)", "new Date(2016, 0, 1, 0, 60, 0)")]
        [InlineData("new Date(2015, 11, 2, 0, 0, 59)", "new Date(2015, 11, 1, 23, 60, 59)")]
        [InlineData("new Date(2012, 2, 1, 1, 0, 0)", "new Date(2012, 2, 1, 0, 60, 0)")]
        [InlineData("new Date(2012, 2, 1, 0, 0, 59)", "new Date(2012, 1, 29, 23, 60, 59)")]
        [InlineData("new Date(2015, 11, 31, 23, 59, 0)", "new Date(2016, 0, 1, 0, -1, 0)")]
        [InlineData("new Date(2015, 11, 1, 22, 59, 59)", "new Date(2015, 11, 1, 23, -1, 59)")]
        [InlineData("new Date(2012, 1, 29, 23, 59, 0)", "new Date(2012, 2, 1, 0, -1, 0)")]
        [InlineData("new Date(2012, 1, 29, 22, 59, 59)", "new Date(2012, 1, 29, 23, -1, 59)")]
        [InlineData("new Date(2016, 0, 2, 15, 5, 0)", "new Date(2016, 0, 1, 0, 2345, 0)")]
        [InlineData("new Date(2015, 11, 3, 14, 5, 59)", "new Date(2015, 11, 1, 23, 2345, 59)")]
        [InlineData("new Date(2012, 2, 2, 15, 5, 0)", "new Date(2012, 2, 1, 0, 2345, 0)")]
        [InlineData("new Date(2012, 2, 2, 14, 5, 59)", "new Date(2012, 1, 29, 23, 2345, 59)")]
        [InlineData("new Date(2015, 11, 25, 18, 24, 0)", "new Date(2016, 0, 1, 0, -8976, 0)")]
        [InlineData("new Date(2015, 10, 25, 17, 24, 59)", "new Date(2015, 11, 1, 23, -8976, 59)")]
        [InlineData("new Date(2012, 1, 23, 18, 24, 0)", "new Date(2012, 2, 1, 0, -8976, 0)")]
        [InlineData("new Date(2012, 1, 23, 17, 24, 59)", "new Date(2012, 1, 29, 23, -8976, 59)")]
        // Seconds
        [InlineData("new Date(2016, 0, 1, 0, 1, 0)", "new Date(2016, 0, 1, 0, 0, 60)")]
        [InlineData("new Date(2015, 11, 2, 0, 0, 0)", "new Date(2015, 11, 1, 23, 59, 60)")]
        [InlineData("new Date(2012, 2, 1, 0, 1, 0)", "new Date(2012, 2, 1, 0, 0, 60)")]
        [InlineData("new Date(2012, 2, 1, 0, 0, 0)", "new Date(2012, 1, 29, 23, 59, 60)")]
        [InlineData("new Date(2015, 11, 31, 23, 59, 59)", "new Date(2016, 0, 1, 0, 0, -1)")]
        [InlineData("new Date(2015, 11, 1, 23, 58, 59)", "new Date(2015, 11, 1, 23, 59, -1)")]
        [InlineData("new Date(2012, 1, 29, 23, 59, 59)", "new Date(2012, 2, 1, 0, 0, -1)")]
        [InlineData("new Date(2012, 1, 29, 23, 58, 59)", "new Date(2012, 1, 29, 23, 59, -1)")]
        [InlineData("new Date(2016, 0, 3, 17, 9, 58)", "new Date(2016, 0, 1, 0, 0, 234598)")]
        [InlineData("new Date(2015, 11, 4, 17, 8, 58)", "new Date(2015, 11, 1, 23, 59, 234598)")]
        [InlineData("new Date(2012, 2, 3, 17, 9, 58)", "new Date(2012, 2, 1, 0, 0, 234598)")]
        [InlineData("new Date(2012, 2, 3, 17, 8, 58)", "new Date(2012, 1, 29, 23, 59, 234598)")]
        [InlineData("new Date(2015, 11, 21, 14, 39, 15)", "new Date(2016, 0, 1, 0, 0, -897645)")]
        [InlineData("new Date(2015, 10, 21, 14, 38, 15)", "new Date(2015, 11, 1, 23, 59, -897645)")]
        [InlineData("new Date(2012, 1, 19, 14, 39, 15)", "new Date(2012, 2, 1, 0, 0, -897645)")]
        [InlineData("new Date(2012, 1, 19, 14, 38, 15)", "new Date(2012, 1, 29, 23, 59, -897645)")]
        public void ShouldSupportDateConsturctorWithArgumentOutOfRange(string expected, string actual)
        {
            var engine = new Engine(o => o.LocalTimeZone(TimeZoneInfo.Utc));
            var expectedValue = engine.Execute(expected).GetCompletionValue().ToObject();
            var actualValue = engine.Execute(actual).GetCompletionValue().ToObject();
            Assert.Equal(expectedValue, actualValue);
        }

        [Fact]
        public void ShouldReturnCorrectConcatenatedStrings()
        {
            RunTest(@"
                function concat(x, a, b) {
                    x += a;
                    x += b;
                    return x;
                }");

            var concat = _engine.GetValue("concat");
            var result = concat.Invoke("concat", "well", "done").ToObject() as string;
            Assert.Equal("concatwelldone", result);
        }

        [Fact]
        public void ComplexMappingAndReducing()
        {
            const string program = @"
Object.map = function (o, f, ctx) {
    ctx = ctx || this;
    var result = [];
    Object.keys(o).forEach(function(k) {
        result.push(f.call(ctx, o[k], k));
	});
    return result;
};

var x1 = {""Value"":1.0,""Elements"":[{""Name"":""a"",""Value"":""b"",""Decimal"":3.2},{""Name"":""a"",""Value"":""b"",""Decimal"": 3.5}],""Values"":{""test"": 2,""test1"":3,""test2"": 4}}
var x2 = {""Value"":2.0,""Elements"":[{""Name"":""aa"",""Value"":""ba"",""Decimal"":3.5}],""Values"":{""test"":1,""test1"":2,""test2"":3}};

function output(x) {
	var elements = x.Elements.map(function(a){return a.Decimal;});
	var values = x.Values;
	var generated = x.Elements.reduce(function(_obj, _cur) {_obj[(function(a){return a.Name;})(_cur)] = (function(a){return a.Decimal;})(_cur);return _obj;}, {});
	return {
        TestDictionary1 : values,
        TestDictionary2 : x.Values,
        TestDictionaryDirectAccess1 : Object.keys(x.Values).length,
        TestDictionaryDirectAccess2 : Object.keys(x.Values),
        TestDictionaryDirectAccess4 : Object.keys(x.Values).map(function(a){return x.Values[a];}),
        TestDictionarySum1 : Object.keys(values).map(function(a){return{Key: a,Value:values[a]};}).map(function(a){return a.Value;}).reduce(function(a, b) { return a + b; }, 0),
        TestDictionarySum2 : Object.keys(x.Values).map(function(a){return{Key: a,Value:x.Values[a]};}).map(function(a){return a.Value;}).reduce(function(a, b) { return a + b; }, 0),
        TestDictionarySum3 : Object.keys(x.Values).map(function(a){return x.Values[a];}).reduce(function(a, b) { return a + b; }, 0),
        TestDictionaryAverage1 : Object.keys(values).map(function(a){return{Key: a,Value:values[a]};}).map(function(a){return a.Value;}).reduce(function(a, b) { return a + b; }, 0)/(Object.keys(values).length||1),
        TestDictionaryAverage2 : Object.keys(x.Values).map(function(a){return{Key: a,Value:x.Values[a]};}).map(function(a){return a.Value;}).reduce(function(a, b) { return a + b; }, 0)/(Object.keys(x.Values).length||1),
        TestDictionaryAverage3 : Object.keys(x.Values).map(function(a){return x.Values[a];}).reduce(function(a, b) { return a + b; }, 0)/(Object.keys(x.Values).map(function(a){return x.Values[a];}).length||1),
        TestDictionaryFunc1 : Object.keys(x.Values).length,
        TestDictionaryFunc2 : Object.map(x.Values, function(v, k){ return v;}),
        TestGeneratedDictionary1 : generated,
        TestGeneratedDictionary2 : x.Elements.reduce(function(_obj, _cur) {_obj[(function(a){return a.Name;})(_cur)] = (function(a){return a.Decimal;})(_cur);return _obj;}, {}),
        TestGeneratedDictionary3 : Object.keys(generated).length,
        TestGeneratedDictionarySum1 : Object.keys(generated).map(function(a){return{Key: a,Value:generated[a]};}).map(function(a){return a.Value;}).reduce(function(a, b) { return a + b; }, 0),
        TestGeneratedDictionarySum2 : Object.keys(x.Elements.reduce(function(_obj, _cur) {_obj[(function(a){return a.Name;})(_cur)] = (function(a){return a.Decimal;})(_cur);return _obj;}, {})).map(function(a){return{Key: a,Value:x.Elements.reduce(function(_obj, _cur) {_obj[(function(a){return a.Name;})(_cur)] = (function(a){return a.Decimal;})(_cur);return _obj;}, {})[a]};}).map(function(a){return a.Value;}).reduce(function(a, b) { return a + b; }, 0),
        TestGeneratedDictionaryAverage1 : Object.keys(generated).map(function(a){return{Key: a,Value:generated[a]};}).map(function(a){return a.Value;}).reduce(function(a, b) { return a + b; }, 0)/(Object.keys(generated).length||1),
        TestGeneratedDictionaryAverage2 : Object.keys(x.Elements.reduce(function(_obj, _cur) {_obj[(function(a){return a.Name;})(_cur)] = (function(a){return a.Decimal;})(_cur);return _obj;}, {})).map(function(a){return{Key: a,Value:x.Elements.reduce(function(_obj, _cur) {_obj[(function(a){return a.Name;})(_cur)] = (function(a){return a.Decimal;})(_cur);return _obj;}, {})[a]};}).map(function(a){return a.Value;}).reduce(function(a, b) { return a + b; }, 0)/(Object.keys(x.Elements.reduce(function(_obj, _cur) {_obj[(function(a){return a.Name;})(_cur)] = (function(a){return a.Decimal;})(_cur);return _obj;}, {})).length||1),
        TestGeneratedDictionaryDirectAccess1 : Object.keys(generated),
        TestGeneratedDictionaryDirectAccess2 : Object.keys(generated).map(function(a){return generated[a];}),
        TestGeneratedDictionaryDirectAccess3 : Object.keys(generated).length,
        TestList1 : elements.reduce(function(a, b) { return a + b; }, 0),
        TestList2 : x.Elements.map(function(a){return a.Decimal;}).reduce(function(a, b) { return a + b; }, 0),
        TestList3 : x.Elements.map(function(a){return a.Decimal;}).reduce(function(a, b) { return a + b; }, 0),
        TestList4 : x.Elements.map(function(a){return a.Decimal;}).reduce(function(a, b) { return a + b; }, 0)/(x.Elements.length||1),
        TestList5 : x.Elements.map(function(a){return a.Decimal;}).reduce(function(a, b) { return a + b; }, 0)/(x.Elements.map((function(a){return a.Decimal;})).length||1)
    };
};
";
            _engine.Execute(program);
            var result1 = (ObjectInstance)_engine.Execute("output(x1)").GetCompletionValue();
            var result2 = (ObjectInstance)_engine.Execute("output(x2)").GetCompletionValue();

            Assert.Equal(9, TypeConverter.ToNumber(result1.Get("TestDictionarySum1")));
            Assert.Equal(9, TypeConverter.ToNumber(result1.Get("TestDictionarySum2")));
            Assert.Equal(9, TypeConverter.ToNumber(result1.Get("TestDictionarySum3")));

            Assert.Equal(3, TypeConverter.ToNumber(result1.Get("TestDictionaryAverage1")));
            Assert.Equal(3, TypeConverter.ToNumber(result1.Get("TestDictionaryAverage2")));
            Assert.Equal(3, TypeConverter.ToNumber(result1.Get("TestDictionaryAverage3")));

            Assert.Equal(3, TypeConverter.ToNumber(result1.Get("TestDictionaryFunc1")));
            Assert.Equal(1, TypeConverter.ToNumber(result1.Get("TestGeneratedDictionary3")));

            Assert.Equal(3.5, TypeConverter.ToNumber(result1.Get("TestGeneratedDictionarySum1")));
            Assert.Equal(3.5, TypeConverter.ToNumber(result1.Get("TestGeneratedDictionarySum2")));
            Assert.Equal(3.5, TypeConverter.ToNumber(result1.Get("TestGeneratedDictionaryAverage1")));
            Assert.Equal(3.5, TypeConverter.ToNumber(result1.Get("TestGeneratedDictionaryAverage2")));

            Assert.Equal(1, TypeConverter.ToNumber(result1.Get("TestGeneratedDictionaryDirectAccess3")));

            Assert.Equal(6.7, TypeConverter.ToNumber(result1.Get("TestList1")));
            Assert.Equal(6.7, TypeConverter.ToNumber(result1.Get("TestList2")));
            Assert.Equal(6.7, TypeConverter.ToNumber(result1.Get("TestList3")));
            Assert.Equal(3.35, TypeConverter.ToNumber(result1.Get("TestList4")));
            Assert.Equal(3.35, TypeConverter.ToNumber(result1.Get("TestList5")));

            Assert.Equal(6, TypeConverter.ToNumber(result2.Get("TestDictionarySum1")));
            Assert.Equal(6, TypeConverter.ToNumber(result2.Get("TestDictionarySum2")));
            Assert.Equal(6, TypeConverter.ToNumber(result2.Get("TestDictionarySum3")));

            Assert.Equal(2, TypeConverter.ToNumber(result2.Get("TestDictionaryAverage1")));
            Assert.Equal(2, TypeConverter.ToNumber(result2.Get("TestDictionaryAverage2")));
            Assert.Equal(2, TypeConverter.ToNumber(result2.Get("TestDictionaryAverage3")));
        }
        [Fact]
        public void ShouldBeAbleToSpreadArrayLiteralsAndFunctionParameters()
        {
            RunTest(@"
                function concat(x, a, b) {
                    x += a;
                    x += b;
                    return x;
                }
                var s = [...'abc'];
                var c = concat(1, ...'ab');
                var arr1 = [1, 2];
                var arr2 = [3, 4 ];
                var r = [...arr2, ...arr1];
            ");

            var arrayInstance = (ArrayInstance)_engine.GetValue("r");
            Assert.Equal(arrayInstance[0], 3);
            Assert.Equal(arrayInstance[1], 4);
            Assert.Equal(arrayInstance[2], 1);
            Assert.Equal(arrayInstance[3], 2);

            arrayInstance = (ArrayInstance)_engine.GetValue("s");
            Assert.Equal(arrayInstance[0], 'a');
            Assert.Equal(arrayInstance[1], 'b');
            Assert.Equal(arrayInstance[2], 'c');

            var c = _engine.GetValue("c").ToString();
            Assert.Equal("1ab", c);
        }

        [Fact]
        public void ShouldSupportDefaultsInFunctionParameters()
        {
            RunTest(@"
                function f(x, y=12) {
                  // y is 12 if not passed (or passed as undefined)
                  return x + y;
                }
            ");

            var function = _engine.GetValue("f");
            var result = function.Invoke(3).ToString();
            Assert.Equal("15", result);

            result = function.Invoke(3, JsValue.Undefined).ToString();
            Assert.Equal("15", result);
        }

        [Fact]
        public void ShouldReportErrorForInvalidJson()
        {
            var engine = new Engine();
            var ex = Assert.Throws<JavaScriptException>(() => engine.Execute("JSON.parse('[01]')"));
            Assert.Equal("Unexpected token '1'", ex.Message);

            var voidCompletion = engine.Execute("try { JSON.parse('01') } catch (e) {}").GetCompletionValue();
            Assert.Equal(JsValue.Undefined, voidCompletion);
        }

        [Fact]
        public void ShouldParseAnonymousToTypeObject()
        {
            var obj = new Wrapper();
            var engine = new Engine()
                .SetValue("x", obj);
            var js = @"
x.test = {
    name: 'Testificate',
    init (a, b) {
        return a + b
    }
}";
            engine.Execute(js);

            Assert.Equal("Testificate", obj.Test.Name);
            Assert.Equal(5, obj.Test.Init(2, 3));
        }

        [Fact]
        public void ShouldOverrideDefaultTypeConverter()
        {
            var engine = new Engine
            {
                ClrTypeConverter = new TestTypeConverter()
            };
            Assert.IsType<TestTypeConverter>(engine.ClrTypeConverter);
            engine.SetValue("x", new Testificate());
            Assert.Throws<JavaScriptException>(() => engine.Execute("c.Name"));
        }

        private class Wrapper
        {
            public Testificate Test { get; set; }
        }

        private class Testificate
        {
            public string Name { get; set; }
            public Func<int, int, int> Init { get; set; }
        }

        private class TestTypeConverter : Jint.Runtime.Interop.ITypeConverter
        {

            public object Convert(object value, Type type, IFormatProvider formatProvider)
            {
                throw new NotImplementedException();
            }

            public bool TryConvert(object value, Type type, IFormatProvider formatProvider, out object converted)
            {
                throw new NotImplementedException();
            }
        }
    }
}
