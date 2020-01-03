
using Esprima.Ast;
using Jint.Runtime.Debugger;

namespace Jint
{

	public partial class Engine
	{

		public delegate void DebugStepDelegateEx(Statement stmt);
		public event DebugStepDelegateEx StepEx;

		public DebugInformation GetDebugInformation(Statement stmt) => stmt !=null ? DebugHandler.CreateDebugInformation(stmt): null;

		public int StatementCount => _statementsCount;

		public string GetCallExpressionString(CallExpression callExpression)
		{
			var identifier = callExpression.Callee as Esprima.Ast.Identifier;
			if (identifier != null)
			{
				var stack = identifier.Name + "(";
				var paramStrings = new System.Collections.Generic.List<string>();

				foreach (var argument in callExpression.Arguments)
				{
					if (argument != null)
					{
						var argIdentifier = argument as Esprima.Ast.Identifier;
						paramStrings.Add(argIdentifier != null ? argIdentifier.Name : "null");
					}
					else
					{
						paramStrings.Add("null");
					}
				}

				stack += string.Join(", ", paramStrings);
				stack += ")";
				return stack;
			}
			return "anonymous function";
		}

	}
}