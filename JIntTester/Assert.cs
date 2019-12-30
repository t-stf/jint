using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace JIntTester
{
	public class Assert
	{

		/// <summary>
		/// Asserts that a condition is true. If the condition is false the method throws
		/// an <see cref="AssertExecption"/>.
		/// </summary>
		/// <param name="condition">The evaluated condition</param>
		public static void True(bool condition)
		{
			if (!condition)
				throw new AssertExecption("True failed");
		}

		/// <summary>
		/// Asserts that a condition is false. If the condition is true the method throws
		/// an <see cref="AssertExecption"/>.
		/// </summary> 
		/// <param name="condition">The evaluated condition</param>
		public static void False(bool condition)
		{
			if (condition)
				throw new AssertExecption("False failed");
		}

		public static void Null(object anObject)
		{
			if (anObject is null)
				return;
			throw new AssertExecption(anObject.ToString());
		}

		public static void NotNull(object anObject)
		{
			if (!(anObject is null))
				return;
			throw new AssertExecption("NotNull failed");
		}

		public static void Fail(string msg)
		{
			throw new AssertExecption(msg);
		}

	}

	/// <summary>
	/// Thrown when an assertion failed.
	/// </summary>
	class AssertExecption : Exception
	{
		public AssertExecption(string msg) : base(msg)
		{
		}



	}

}
