using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE ORIGINAL PAINTS A MODEL'S FACES IN PROGRAM ORDER (2026-11-18, per user with the chest in both games: the
	/// original's shows broad grey-blue bands on its edges and the lid reads square, ours a round wooden lid with thin
	/// lines). The model interpreter (INTERP.ASM) has no depth buffer: a back-face test (jnorm, UW3DModelSideTest) opens a
	/// region, the faces in it are drawn one over the other in the order the program lists them, the later on top. The
	/// chest's metal bands follow their wooden face in the same region and lie in its plane or up to two units under it
	/// (the long side's band at a constant x, the wood leaning out with height), so with our depth buffer the wood won.
	///
	/// Rebuilt for the mesh: a face lying in the plane of its innermost back-face test (every corner within PlaneTolerance)
	/// is put exactly onto that plane and lifted off it by RankOffset for each face before it in the same region, along
	/// the side the test shows. A face that does not lie in its test's plane - a test guarding a group of faces facing
	/// different ways - stays as it is.
	///
	/// ACROSS REGIONS TOO (per user the same day: the chest's corner band running down its end showed a wedge of wood):
	/// the corner bands have back-face tests of their own, planes tilted a little against the wood they lie on, and come
	/// later in the program - so the original paints them over the wood, while our depth buffer let the wood win below the
	/// line where the two planes cross. A later face whose plane is nearly that of an earlier one (normals within
	/// ParallelCosine, every corner within PlaneTolerance of it) is pushed to lie at least RankOffset in front of it.
	/// </summary>
	public static class UW3DModelLayering
	{
		/// <summary>How far a corner may lie off the test's plane and still count as in it: four model units (a tile is 256).</summary>
		public const float PlaneTolerance = 4f / 256f;

		/// <summary>The lift per face painted before it in the same region: a third of a model unit.</summary>
		public const float RankOffset = 0.35f / 256f;

		/// <summary>Two faces' planes count as one surface when their normals are this close (about eleven degrees).</summary>
		public const double ParallelCosine = 0.98;

		private struct LaidFace
		{
			public UWVector3[] Corners;

			public double NX, NY, NZ;
		}

		/// <summary>
		/// The corners of every face as the mesh should take them, in the model's units (X, Y, Z with Z the height): laid on
		/// their test's plane and layered by program order where they lie in it, else the model's own vertices.
		/// </summary>
		public static List<UWVector3[]> FaceCorners(UW3DModel pOModel)
		{
			List<UWVector3[]> lOResult = new List<UWVector3[]>(pOModel.Faces.Count);
			Dictionary<UW3DModelSideTest, int> lOPainted = new Dictionary<UW3DModelSideTest, int>();
			List<LaidFace> lOLaid = new List<LaidFace>();

			foreach (UW3DModelFace lOFace in pOModel.Faces)
			{
				UWVector3[] lOCorners = new UWVector3[lOFace.VertexIndices.Count];

				for (int liAt = 0; liAt < lOCorners.Length; liAt++)
					lOCorners[liAt] = pOModel.Vertices[lOFace.VertexIndices[liAt]];

				UW3DModelSideTest lOTest = fInnermostPlane(lOFace);

				if (lOTest != null && fPlane(lOTest, out double ldNX, out double ldNY, out double ldNZ, out double ldD)
					&& fAllNear(lOCorners, ldNX, ldNY, ldNZ, ldD))
				{
					lOPainted.TryGetValue(lOTest, out int liBefore);
					lOPainted[lOTest] = liBefore + 1;

					double ldLift = liBefore * RankOffset * (lOTest.Else ? -1 : 1);

					for (int liAt = 0; liAt < lOCorners.Length; liAt++)
					{
						UWVector3 lV = lOCorners[liAt];
						double ldMove = ldLift - (ldNX * lV.X + ldNY * lV.Y + ldNZ * lV.Z + ldD);

						lOCorners[liAt] = new UWVector3((float)(lV.X + ldNX * ldMove), (float)(lV.Y + ldNY * ldMove), (float)(lV.Z + ldNZ * ldMove));
					}

					// Over every earlier face of nearly the same surface, whatever its region.
					foreach (LaidFace lOEarlier in lOLaid)
						fPaintOver(lOCorners, ldNX, ldNY, ldNZ, lOEarlier);

					lOLaid.Add(new LaidFace { Corners = lOCorners, NX = ldNX, NY = ldNY, NZ = ldNZ });
				}

				lOResult.Add(lOCorners);
			}

			return lOResult;
		}

		/// <summary>A later face on nearly the same surface as pOEarlier comes to lie at least RankOffset in front of it,
		/// along the earlier face's normal (corner by corner; the plane through its first corner).</summary>
		private static void fPaintOver(UWVector3[] pOCorners, double pdNX, double pdNY, double pdNZ, LaidFace pOEarlier)
		{
			if (pdNX * pOEarlier.NX + pdNY * pOEarlier.NY + pdNZ * pOEarlier.NZ < ParallelCosine)
				return;

			UWVector3 lOOn = pOEarlier.Corners[0];
			double ldD = -(pOEarlier.NX * lOOn.X + pOEarlier.NY * lOOn.Y + pOEarlier.NZ * lOOn.Z);

			foreach (UWVector3 lV in pOCorners)
				if (Math.Abs(pOEarlier.NX * lV.X + pOEarlier.NY * lV.Y + pOEarlier.NZ * lV.Z + ldD) > PlaneTolerance)
					return;

			for (int liAt = 0; liAt < pOCorners.Length; liAt++)
			{
				UWVector3 lV = pOCorners[liAt];
				double ldShort = RankOffset - (pOEarlier.NX * lV.X + pOEarlier.NY * lV.Y + pOEarlier.NZ * lV.Z + ldD);

				if (ldShort > 0)
					pOCorners[liAt] = new UWVector3((float)(lV.X + pOEarlier.NX * ldShort), (float)(lV.Y + pOEarlier.NY * ldShort), (float)(lV.Z + pOEarlier.NZ * ldShort));
			}
		}

		/// <summary>The face's innermost back-face test (not a register skip), or null.</summary>
		private static UW3DModelSideTest fInnermostPlane(UW3DModelFace pOFace)
		{
			if (pOFace.SideTests == null)
				return null;

			for (int liAt = pOFace.SideTests.Count - 1; liAt >= 0; liAt--)
				if (!pOFace.SideTests[liAt].Register)
					return pOFace.SideTests[liAt];

			return null;
		}

		/// <summary>
		/// The test's plane in the model's units, the unit normal pointing to the side the test shows: the interpreter's
		/// x, y, z are our X, Z (height), Y, and the test holds where n * (p + eye) is not negative, so the plane is
		/// n . (v * 256 + p) = 0.
		/// </summary>
		private static bool fPlane(UW3DModelSideTest pOTest, out double pdNX, out double pdNY, out double pdNZ, out double pdD)
		{
			double ldX = pOTest.NX, ldY = pOTest.NZ, ldZ = pOTest.NY;
			double ldLength = Math.Sqrt(ldX * ldX + ldY * ldY + ldZ * ldZ);

			pdNX = pdNY = pdNZ = pdD = 0;

			if (ldLength < 1)
				return false;

			pdNX = ldX / ldLength;
			pdNY = ldY / ldLength;
			pdNZ = ldZ / ldLength;
			pdD = (pdNX * pOTest.PX + pdNY * pOTest.PZ + pdNZ * pOTest.PY) / 256.0;

			return true;
		}

		private static bool fAllNear(UWVector3[] pOCorners, double pdNX, double pdNY, double pdNZ, double pdD)
		{
			foreach (UWVector3 lV in pOCorners)
				if (Math.Abs(pdNX * lV.X + pdNY * lV.Y + pdNZ * lV.Z + pdD) > PlaneTolerance)
					return false;

			return true;
		}
	}
}
