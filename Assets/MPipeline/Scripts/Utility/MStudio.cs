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

	public class Spring2
	{
		public Vector2 value = Vector2.zero;
		private Vector2 dampValue = Vector2.zero;
		private float damp = 1;
		private float frequence = 1;

		public void Clear ()
		{
			value = Vector2.zero;
			dampValue = Vector2.zero;
		}

		public Spring2 (float damp, float frequence)
		{
			this.damp = damp;
			this.frequence = frequence;
		}

		public void Update (float deltaTime)
		{
			value -= dampValue * deltaTime * frequence;
			dampValue = Vector2.Lerp (dampValue, value, deltaTime * damp);
		}

		public void Update (float deltaTime, Vector2 target)
		{
			value -= dampValue * deltaTime * frequence;
			dampValue = Vector2.Lerp (dampValue, value - target, deltaTime * damp);
		}
	}

	public class Spring3
	{
		public Vector3 value = Vector2.zero;
		private Vector3 dampValue = Vector2.zero;
		private float damp = 1;
		private float frequence = 1;

		public void Clear ()
		{
			value = Vector2.zero;
			dampValue = Vector2.zero;
		}

		public Spring3 (float damp, float frequence)
		{
			this.damp = damp;
			this.frequence = frequence;
		}

		public void Update (float deltaTime, Vector3 target)
		{
			value -= dampValue * deltaTime * frequence;

			dampValue = Vector3.Lerp (dampValue, value - target, deltaTime * damp);

		}


		public void Update (float deltaTime)
		{
			value -= dampValue * deltaTime * frequence;

			dampValue = Vector3.Lerp (dampValue, value, deltaTime * damp);

		}
	}

	public class SpringEulerAngle
	{
		public Vector3 value = Vector2.zero;
		private Vector3 dampValue = Vector2.zero;
		private float damp;
		private float frequence = 1;
		private Vector3 lerpTarget;

		public SpringEulerAngle (float damp, float frequence)
		{
			this.damp = damp;
			this.frequence = frequence;
		}

		public void Clear ()
		{
			value = Vector2.zero;
			dampValue = Vector2.zero;
			lerpTarget = Vector2.zero;
		}

		public void Update (float deltaTime, Vector3 target)
		{

			value -= dampValue * deltaTime * frequence;
			lerpTarget = Quaternion.Slerp (Quaternion.Euler (lerpTarget), Quaternion.Euler (target), deltaTime * frequence).eulerAngles;
			dampValue.x = Mathf.LerpAngle (dampValue.x, value.x - target.x, deltaTime * damp);
			dampValue.y = Mathf.LerpAngle (dampValue.y, value.y - target.y, deltaTime * damp);
			dampValue.z = Mathf.LerpAngle (dampValue.z, value.z - target.z, deltaTime * damp);

		}

		public void Update (float deltaTime)
		{
			value -= dampValue * deltaTime * frequence;
			dampValue.x = Mathf.LerpAngle (dampValue.x, value.x, deltaTime * damp);
			dampValue.y = Mathf.LerpAngle (dampValue.y, value.y, deltaTime * damp);
			dampValue.z = Mathf.LerpAngle (dampValue.z, value.z, deltaTime * damp);
		}
	}

	public class Json
	{
		public static string JsonToString<T> (T value)
		{  
            
			using (var vStream = new MemoryStream ()) {  
				DataContractJsonSerializer serializer = new DataContractJsonSerializer (typeof(T));  
				serializer.WriteObject (vStream, value);  
				byte[] jsondata = new byte[vStream.Length];  
				vStream.Position = 0;  
				if (vStream.Read (jsondata, 0, jsondata.Length) != jsondata.Length)
					throw new Exception ("Wrong Reading");  
				return Encoding.UTF8.GetString (jsondata);  
			}  
		}

		public static T StringToJson<T> (string text)
		{  
			byte[] jsondata = Encoding.UTF8.GetBytes (text);  
			DataContractJsonSerializer serializer = new DataContractJsonSerializer (typeof(T));  
			using (var vStream = new MemoryStream (jsondata)) {  
				return (T)serializer.ReadObject (vStream);  
			}  
		}

		public static T BytesToJson<T> (byte[] jsondata)
		{   
			DataContractJsonSerializer serializer = new DataContractJsonSerializer (typeof(T));  
			using (var vStream = new MemoryStream (jsondata)) {  
				return (T)serializer.ReadObject (vStream);  
			}  
		}

		public static byte[] JsonToBytes<T> (T value)
		{  
			using (var vStream = new MemoryStream ()) {  
				DataContractJsonSerializer serializer = new DataContractJsonSerializer (typeof(T));  
				serializer.WriteObject (vStream, value);  
				byte[] jsondata = new byte[vStream.Length];  
				vStream.Position = 0;  
				if (vStream.Read (jsondata, 0, jsondata.Length) != jsondata.Length)
					throw new Exception ("Wrong Reading");  
				return jsondata;
			}  
		}
	}

	[Serializable]
	public class PerlinDistribution
	{

		public float scale;
		private int m_seed;

		public int seed {
			get { return m_seed; }
			set {
				if (value == 0) {
					s_perm = new byte[s_perm_ORIGINAL.Length];
					s_perm_ORIGINAL.CopyTo (s_perm, 0);
				} else {
					s_perm = new byte[512];
					var random = new Random (value);
					random.NextBytes (s_perm);
					m_seed = value;
				}
			}
		}

		public PerlinDistribution ()
		{
			seed = Guid.NewGuid ().GetHashCode ();
		}

		public float ValueAt (float x)
		{
			return Generate (x * scale);
		}

		public float ValueAt (float x, float y)
		{
			return Generate (x * scale, y * scale);
		}

		public float ValueAt (float x, float y, float z)
		{
			return Generate (x * scale, y * scale, z * scale);
		}

		internal float Generate (float x)
		{
			var i0 = FastFloor (x);
			var i1 = i0 + 1;
			var x0 = x - i0;
			var x1 = x0 - 1.0f;

			float n0, n1;

			var t0 = 1.0f - x0 * x0;
			t0 *= t0;
			n0 = t0 * t0 * Grad (s_perm [i0 & 0xff], x0);

			var t1 = 1.0f - x1 * x1;
			t1 *= t1;
			n1 = t1 * t1 * Grad (s_perm [i1 & 0xff], x1);
			return 0.395f * (n0 + n1);
		}

		internal float Generate (float x, float y)
		{
			const float f2 = 0.366025403f; // f2 = 0.5*(sqrt(3.0)-1.0)
			const float g2 = 0.211324865f; // g2 = (3.0-Math.sqrt(3.0))/6.0

			float n0, n1, n2;

			var s = (x + y) * f2;
			var xs = x + s;
			var ys = y + s;
			var i = FastFloor (xs);
			var j = FastFloor (ys);

			var t = (i + j) * g2;
			var X0 = i - t;
			var Y0 = j - t;
			var x0 = x - X0;
			var y0 = y - Y0;

			int i1, j1;
			if (x0 > y0) {
				i1 = 1;
				j1 = 0;
			} else {
				i1 = 0;
				j1 = 1;
			}

			var x1 = x0 - i1 + g2;
			var y1 = y0 - j1 + g2;
			var x2 = x0 - 1.0f + 2.0f * g2;
			var y2 = y0 - 1.0f + 2.0f * g2;

			var ii = i % 256;
			var jj = j % 256;

			var t0 = 0.5f - x0 * x0 - y0 * y0;
			if (t0 < 0.0f)
				n0 = 0.0f;
			else {
				t0 *= t0;
				n0 = t0 * t0 * Grad (s_perm [ii + s_perm [jj]], x0, y0);
			}

			var t1 = 0.5f - x1 * x1 - y1 * y1;
			if (t1 < 0.0f)
				n1 = 0.0f;
			else {
				t1 *= t1;
				n1 = t1 * t1 * Grad (s_perm [ii + i1 + s_perm [jj + j1]], x1, y1);
			}

			var t2 = 0.5f - x2 * x2 - y2 * y2;
			if (t2 < 0.0f)
				n2 = 0.0f;
			else {
				t2 *= t2;
				n2 = t2 * t2 * Grad (s_perm [ii + 1 + s_perm [jj + 1]], x2, y2);
			}

			return 20.0f * (n0 + n1 + n2) + 0.5f;
		}

		internal float Generate (float x, float y, float z)
		{
			const float F3 = 0.333333333f;
			const float G3 = 0.166666667f;

			float n0, n1, n2, n3;


			var s = (x + y + z) * F3;
			var xs = x + s;
			var ys = y + s;
			var zs = z + s;
			var i = FastFloor (xs);
			var j = FastFloor (ys);
			var k = FastFloor (zs);

			var t = (float)(i + j + k) * G3;
			var X0 = i - t; 
			var Y0 = j - t;
			var Z0 = k - t;
			var x0 = x - X0; 
			var y0 = y - Y0;
			var z0 = z - Z0;


			int i1, j1, k1; 
			int i2, j2, k2;


			if (x0 >= y0) {
				if (y0 >= z0) {
					i1 = 1;
					j1 = 0;
					k1 = 0;
					i2 = 1;
					j2 = 1;
					k2 = 0;
				} else if (x0 >= z0) {
					i1 = 1;
					j1 = 0;
					k1 = 0;
					i2 = 1;
					j2 = 0;
					k2 = 1;
				} else {
					i1 = 0;
					j1 = 0;
					k1 = 1;
					i2 = 1;
					j2 = 0;
					k2 = 1;
				}
			} else { 
				if (y0 < z0) {
					i1 = 0;
					j1 = 0;
					k1 = 1;
					i2 = 0;
					j2 = 1;
					k2 = 1;
				} else if (x0 < z0) {
					i1 = 0;
					j1 = 1;
					k1 = 0;
					i2 = 0;
					j2 = 1;
					k2 = 1;
				} else {
					i1 = 0;
					j1 = 1;
					k1 = 0;
					i2 = 1;
					j2 = 1;
					k2 = 0;
				}
			}

			var x1 = x0 - i1 + G3; 
			var y1 = y0 - j1 + G3;
			var z1 = z0 - k1 + G3;
			var x2 = x0 - i2 + 2.0f * G3;
			var y2 = y0 - j2 + 2.0f * G3;
			var z2 = z0 - k2 + 2.0f * G3;
			var x3 = x0 - 1.0f + 3.0f * G3;
			var y3 = y0 - 1.0f + 3.0f * G3;
			var z3 = z0 - 1.0f + 3.0f * G3;

			var ii = Mod (i, 256);
			var jj = Mod (j, 256);
			var kk = Mod (k, 256);

			var t0 = 0.6f - x0 * x0 - y0 * y0 - z0 * z0;
			if (t0 < 0.0f)
				n0 = 0.0f;
			else {
				t0 *= t0;
				n0 = t0 * t0 * Grad (s_perm [ii + s_perm [jj + s_perm [kk]]], x0, y0, z0);
			}

			var t1 = 0.6f - x1 * x1 - y1 * y1 - z1 * z1;
			if (t1 < 0.0f)
				n1 = 0.0f;
			else {
				t1 *= t1;
				n1 = t1 * t1 * Grad (s_perm [ii + i1 + s_perm [jj + j1 + s_perm [kk + k1]]], x1, y1, z1);
			}

			var t2 = 0.6f - x2 * x2 - y2 * y2 - z2 * z2;
			if (t2 < 0.0f)
				n2 = 0.0f;
			else {
				t2 *= t2;
				n2 = t2 * t2 * Grad (s_perm [ii + i2 + s_perm [jj + j2 + s_perm [kk + k2]]], x2, y2, z2);
			}

			var t3 = 0.6f - x3 * x3 - y3 * y3 - z3 * z3;
			if (t3 < 0.0f)
				n3 = 0.0f;
			else {
				t3 *= t3;
				n3 = t3 * t3 * Grad (s_perm [ii + 1 + s_perm [jj + 1 + s_perm [kk + 1]]], x3, y3, z3);
			}

			return 16.0f * (n0 + n1 + n2 + n3) + 0.5f;
		}

		private byte[] s_perm;

		private static readonly byte[] s_perm_ORIGINAL = {
			151, 160, 137, 91, 90, 15,
			131, 13, 201, 95, 96, 53, 194, 233, 7, 225, 140, 36, 103, 30, 69, 142, 8, 99, 37, 240, 21, 10, 23,
			190, 6, 148, 247, 120, 234, 75, 0, 26, 197, 62, 94, 252, 219, 203, 117, 35, 11, 32, 57, 177, 33,
			88, 237, 149, 56, 87, 174, 20, 125, 136, 171, 168, 68, 175, 74, 165, 71, 134, 139, 48, 27, 166,
			77, 146, 158, 231, 83, 111, 229, 122, 60, 211, 133, 230, 220, 105, 92, 41, 55, 46, 245, 40, 244,
			102, 143, 54, 65, 25, 63, 161, 1, 216, 80, 73, 209, 76, 132, 187, 208, 89, 18, 169, 200, 196,
			135, 130, 116, 188, 159, 86, 164, 100, 109, 198, 173, 186, 3, 64, 52, 217, 226, 250, 124, 123,
			5, 202, 38, 147, 118, 126, 255, 82, 85, 212, 207, 206, 59, 227, 47, 16, 58, 17, 182, 189, 28, 42,
			223, 183, 170, 213, 119, 248, 152, 2, 44, 154, 163, 70, 221, 153, 101, 155, 167, 43, 172, 9,
			129, 22, 39, 253, 19, 98, 108, 110, 79, 113, 224, 232, 178, 185, 112, 104, 218, 246, 97, 228,
			251, 34, 242, 193, 238, 210, 144, 12, 191, 179, 162, 241, 81, 51, 145, 235, 249, 14, 239, 107,
			49, 192, 214, 31, 181, 199, 106, 157, 184, 84, 204, 176, 115, 121, 50, 45, 127, 4, 150, 254,
			138, 236, 205, 93, 222, 114, 67, 29, 24, 72, 243, 141, 128, 195, 78, 66, 215, 61, 156, 180,
			151, 160, 137, 91, 90, 15,
			131, 13, 201, 95, 96, 53, 194, 233, 7, 225, 140, 36, 103, 30, 69, 142, 8, 99, 37, 240, 21, 10, 23,
			190, 6, 148, 247, 120, 234, 75, 0, 26, 197, 62, 94, 252, 219, 203, 117, 35, 11, 32, 57, 177, 33,
			88, 237, 149, 56, 87, 174, 20, 125, 136, 171, 168, 68, 175, 74, 165, 71, 134, 139, 48, 27, 166,
			77, 146, 158, 231, 83, 111, 229, 122, 60, 211, 133, 230, 220, 105, 92, 41, 55, 46, 245, 40, 244,
			102, 143, 54, 65, 25, 63, 161, 1, 216, 80, 73, 209, 76, 132, 187, 208, 89, 18, 169, 200, 196,
			135, 130, 116, 188, 159, 86, 164, 100, 109, 198, 173, 186, 3, 64, 52, 217, 226, 250, 124, 123,
			5, 202, 38, 147, 118, 126, 255, 82, 85, 212, 207, 206, 59, 227, 47, 16, 58, 17, 182, 189, 28, 42,
			223, 183, 170, 213, 119, 248, 152, 2, 44, 154, 163, 70, 221, 153, 101, 155, 167, 43, 172, 9,
			129, 22, 39, 253, 19, 98, 108, 110, 79, 113, 224, 232, 178, 185, 112, 104, 218, 246, 97, 228,
			251, 34, 242, 193, 238, 210, 144, 12, 191, 179, 162, 241, 81, 51, 145, 235, 249, 14, 239, 107,
			49, 192, 214, 31, 181, 199, 106, 157, 184, 84, 204, 176, 115, 121, 50, 45, 127, 4, 150, 254,
			138, 236, 205, 93, 222, 114, 67, 29, 24, 72, 243, 141, 128, 195, 78, 66, 215, 61, 156, 180
		};

		private static int FastFloor (float x)
		{
			return x > 0 ? (int)x : (int)x - 1;
		}

		private static float Grad (int hash, float x)
		{
			var h = hash & 15;
			var Grad = 1.0f + (h & 7);
			if ((h & 8) != 0)
				Grad = -Grad;
			return Grad * x;
		}

		private static float Grad (int hash, float x, float y)
		{
			var h = hash & 7;
			var u = h < 4 ? x : y;
			var v = h < 4 ? y : x;
			return ((h & 1) != 0 ? -u : u) + ((h & 2) != 0 ? -2.0f * v : 2.0f * v);
		}

		private static float Grad (int hash, float x, float y, float z)
		{
			var h = hash & 15;     
			var u = h < 8 ? x : y; 
			var v = h < 4 ? y : h == 12 || h == 14 ? x : z; 
			return ((h & 1) != 0 ? -u : u) + ((h & 2) != 0 ? -v : v);
		}

		private static float Grad (int hash, float x, float y, float z, float t)
		{
			var h = hash & 31;     
			var u = h < 24 ? x : y;
			var v = h < 16 ? y : z;
			var w = h < 8 ? z : t;
			return ((h & 1) != 0 ? -u : u) + ((h & 2) != 0 ? -v : v) + ((h & 4) != 0 ? -w : w);
		}

		private static int Mod (int x, int m)
		{
			var a = x % m;
			return a < 0 ? a + m : a;
		}
	}

	public static class Utility
	{
		public static void Iterate<T> (this IEnumerable<T> objs, Action<T> func)
		{
			foreach (T t in objs) {
				func (t);
			}
		}

		public static void IteInRange<T> (this IList<T> list, Action<int,T> func)
		{
			for (int i = 0; i < list.Count; ++i) {
				func (i, list [i]);
			}
		}

		public static bool InRange (this Vector3 vec, Vector3 target, float range)
		{
			return Vector3.SqrMagnitude (vec - target) < range * range;
		}

		public static void Random<T> (this IList<T> list)
		{
			Random r = new Random (Guid.NewGuid ().GetHashCode ());
			for (int i = 0; i < list.Count; ++i) {
				int randomIndex = (int)(r.NextDouble () * list.Count);
				var temp = list [randomIndex];
				list [randomIndex] = list [i];
				list [i] = temp;
			}
		}

		public static void Init<T> (this IList<T> objs, Func<T> func)
		{
			for (int i = 0; i < objs.Count; ++i) {
				objs [i] = func ();
			}
		}

		public static void to (this int min, int max, Action<int> func)
		{
			for (int i = min; i < max; ++i) {
				func (i);
			}
		}

		public static void to (this int min, int max, Action func)
		{
			for (int i = min; i < max; ++i) {
				func ();
			}
		}

		public static void Init<T> (this T[][] values, int x, int y)
		{
			values = new T[x][];
			for (int i = 0; i < x; ++i) {
				values [i] = new T[y];
				for (int a = 0; a < y; ++a) {
					values [i] [a] = default(T);
				}
			}
		}

		public static void Init<T> (this T[][] values, int x, int y, T defaultValue)
		{
			values = new T[x][];
			for (int i = 0; i < x; ++i) {
				values [i] = new T[y];
				for (int a = 0; a < y; ++a) {
					values [i] [a] = defaultValue;
				}
			}
		}

		public static void Init<T> (this T[] values, int x, T defaultValue)
		{
			values = new T[x];
			for (int i = 0; i < x; ++i) {
				values [i] = defaultValue;
			}
		}

		public static void Init<T> (this T[] values, int x)
		{
			values = new T[x];
			for (int i = 0; i < x; ++i) {
				values [i] = default(T);
			}
		}
	}
}