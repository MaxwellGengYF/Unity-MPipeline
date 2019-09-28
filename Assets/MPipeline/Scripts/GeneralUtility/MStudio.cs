using System.Collections.Generic;
using System;
using Random = System.Random;
using UnityEngine;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.Serialization.Json;
using System.IO;
namespace MStudio
{

	public static class Library
	{
        public static bool Contains<T>(this List<T> lst, T value, Func<T, T, bool> equalFunc)
        {
            foreach(var i in lst)
            {
                if (equalFunc(value, i)) return true;
            }
            return false;
        }
		public static double GaussianDistribution ()
		{
			var u1 = random.NextDouble ();
			var u2 = random.NextDouble ();

			var randStdNormal = Math.Sqrt (-2.0 * Math.Log (u1)) *
			                     Math.Sin (2.0 * Math.PI * u2);
			return randStdNormal / 12.5663706144 + 0.5;
			
		}

		public static double GaussianDistribution (double x, double s, double o, double u)
		{
			var sqro = o * o;
			return 1d / Math.Sqrt (6.283185307 * sqro) * Math.Exp (-(Math.Pow (s * x - s * u, 2d) / (2d * sqro)));
		}

		static Random random = new Random (Guid.NewGuid ().GetHashCode ());

		public static string GetGuid ()
		{
			return System.Convert.ToBase64String (System.Guid.NewGuid ().ToByteArray ()).Substring (0, 22);
		}

		public static double Random0To1 {
			get { 
				return random.NextDouble ();
			}
		}
		/// <summary>
		/// Sigmoid Function
		/// </summary>
		/// <param name="value">Value.</param>
		public static double Sigmoid(double value){
			return 1 / (1 + Math.Exp (-value));
		}
		/// <summary>
		/// Sigmoid Function From -1 to 1
		/// </summary>
		/// <returns>The curved.</returns>
		/// <param name="value">Value.</param>
		public static double SigmoidCurved(double value){
			return 2 / (1 + Math.Exp (-value)) - 1;
		}

		/// <summary>
		/// Sigmoid Function
		/// </summary>
		/// <param name="value">Value.</param>
		public static float Sigmoid(float value){
			return (float)(1 / (1 + Math.Exp (-value)));
		}

		public static float SigmoidCurved(float value){
			return (float)(2 / (1 + Math.Exp (-value)) - 1);
		}

		public static float Random (float min, float max)
		{
			double d = random.NextDouble ();
			return (float)(d * (max - min) + min);
		}

		public static bool Random ()
		{
			double d = random.NextDouble ();
			return d >= 0.5d;
		}

		public static int Random (int min, int max)
		{
			double d = random.NextDouble ();
			return (int)(d * (max - min) + min);
		}

		public static float Cubic_Interpolate (float value1, float value2, float value3, float value4, float x)
		{
			float p = (value4 - value3) - (value1 - value2);
			float q = (value1 - value2) - p;
			float r = value3 - value1;
			return p * Mathf.Pow (x, 3) + q * Mathf.Pow (x, 2) + r * x + value2;
		}

		public static Vector3 Cubic_Interpolate (Vector3 value1, Vector3 value2, Vector3 value3, Vector3 value4, float x)
		{
			Vector3 p = (value4 - value3) - (value1 - value2);
			Vector3 q = (value1 - value2) - p;
			Vector3 r = value3 - value1;
			return p * Mathf.Pow (x, 3) + q * Mathf.Pow (x, 2) + r * x + value2;
		}

		public static Vector2 Cubic_Interpolate (Vector2 value1, Vector2 value2, Vector2 value3, Vector2 value4, float x)
		{
			Vector2 p = (value4 - value3) - (value1 - value2);
			Vector2 q = (value1 - value2) - p;
			Vector2 r = value3 - value1;
			return p * Mathf.Pow (x, 3) + q * Mathf.Pow (x, 2) + r * x + value2;
		}

		public static byte[] AesEncrypt (byte[] toEncryptArray, string key)
		{

			byte[] keyArray = UTF8Encoding.UTF8.GetBytes (key);
			RijndaelManaged rDel = new RijndaelManaged ();
			rDel.Key = keyArray;
			rDel.Mode = CipherMode.ECB;
			rDel.Padding = PaddingMode.PKCS7;
			ICryptoTransform cTransform = rDel.CreateEncryptor ();
			byte[] resultArray = cTransform.TransformFinalBlock (toEncryptArray, 0, toEncryptArray.Length);
			return resultArray;
		}

		public static byte[] AesDecrypt (byte[] toEncryptArray, string key)
		{

			byte[] keyArray = UTF8Encoding.UTF8.GetBytes (key);
			RijndaelManaged rDel = new RijndaelManaged ();
			rDel.Key = keyArray;
			rDel.Mode = CipherMode.ECB;
			rDel.Padding = PaddingMode.PKCS7;
			ICryptoTransform cTransform = rDel.CreateDecryptor ();
			byte[] resultArray = cTransform.TransformFinalBlock (toEncryptArray, 0, toEncryptArray.Length);
			return resultArray;

		}

		public static LinearFunction Regression (ICollection<Vector2> parray)
		{
			LinearFunction func = new LinearFunction ();
			if (parray.Count < 2) {
				return null;
			}
			double averagex = 0, averagey = 0;
			foreach (Vector2 p in parray) {
				averagex += p.x;
				averagey += p.y;
			}
			averagex /= parray.Count;
			averagey /= parray.Count;
			double numerator = 0;
			double denominator = 0;
			foreach (Vector2 p in parray) {
				numerator += (p.x - averagex) * (p.y - averagey);
				denominator += (p.x - averagex) * (p.x - averagex);
			}
			double RCB = numerator / denominator;
			double RCA = averagey - RCB * averagex;
			func.b = RCA;
			func.k = RCB;
			double residualSS = 0;  //（Residual Sum of Squares）
			double regressionSS = 0; //（Regression Sum of Squares）
			foreach (Vector2 p in parray) {
				residualSS +=
					(p.y - RCA - RCB * p.x) *
				(p.y - RCA - RCB * p.x);
				regressionSS +=
					(RCA + RCB * p.x - averagey) *
				(RCA + RCB * p.x - averagey);
			}
			func.residualSS = residualSS;
			func.regressionSS = regressionSS;
			return func;
		}

		public class LinearFunction
		{
			public double k;
			public double b;
			public double residualSS;
			//剩余平方和
			public double regressionSS;
			//回归平方和
		}
	}
}