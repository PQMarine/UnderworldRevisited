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
	///
	/// ONLY A FACE THAT COVERS AN EARLIER ONE MOVES (per user 2026-11-22: thin cracks in the boulders and the shrine, the
	/// background showing through; the original has none). Until then every face lying in its test's plane was put onto
	/// that plane and every later face of the region lifted - the boulders' facets of two or three triangles in one plane
	/// too, which lie side by side and cover nothing: 39 of the medium boulder's 49 faces moved, up to four model units,
	/// and a corner two faces share went to two places. Since the faces became one-sided (FaceFronts, 2026-11-22) the
	/// cracks showed the background. Now a face keeps the model's corners unless it overlaps an earlier face of nearly the
	/// same surface (the chest's bands on its wood); such a face goes onto its test's plane and then at least RankOffset in
	/// front of every face it covers, measured from that face's own plane.
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
			/// <summary>The corners as laid (an overlay moved, else the model's).</summary>
			public UWVector3[] Corners;

			/// <summary>The model's own corners, for the overlap test.</summary>
			public UWVector3[] Original;

			/// <summary>The test's plane normal, towards the side the test shows.</summary>
			public double NX, NY, NZ;

			/// <summary>The face's own plane as laid (Newell's normal turned to the test's side, through the corners' mean).</summary>
			public double ONX, ONY, ONZ, OD;

			/// <summary>How far its frontmost corner stands in front of that plane (0 for a flat face): a face of more
			/// corners than three need not be flat, and a later face must be in front of all of it.</summary>
			public double Above;
		}

		/// <summary>
		/// The corners of every face as the mesh should take them, in the model's units (X, Y, Z with Z the height): laid on
		/// their test's plane and layered by program order where they lie in it, else the model's own vertices.
		/// </summary>
		public static List<UWVector3[]> FaceCorners(UW3DModel pOModel)
		{
			List<UWVector3[]> lOResult = new List<UWVector3[]>(pOModel.Faces.Count);
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
					double ldSide = lOTest.Else ? -1 : 1;
					UWVector3[] lOOriginal = (UWVector3[])lOCorners.Clone();
					List<LaidFace> lOUnder = new List<LaidFace>();

					foreach (LaidFace lOEarlier in lOLaid)
						if (fSameSurface(lOOriginal, ldNX * ldSide, ldNY * ldSide, ldNZ * ldSide, lOEarlier)
							&& fOverlap(lOOriginal, lOEarlier.Original, lOEarlier.ONX, lOEarlier.ONY, lOEarlier.ONZ))
							lOUnder.Add(lOEarlier);

					// AN OVERLAY: onto its test's plane, then in front of every face it covers.
					if (lOUnder.Count > 0)
					{
						for (int liAt = 0; liAt < lOCorners.Length; liAt++)
						{
							UWVector3 lV = lOCorners[liAt];
							double ldMove = -(ldNX * lV.X + ldNY * lV.Y + ldNZ * lV.Z + ldD);

							lOCorners[liAt] = new UWVector3((float)(lV.X + ldNX * ldMove), (float)(lV.Y + ldNY * ldMove), (float)(lV.Z + ldNZ * ldMove));
						}

						foreach (LaidFace lOEarlier in lOUnder)
							fPaintOver(lOCorners, lOEarlier);
					}

					lOLaid.Add(fLay(lOCorners, lOOriginal, ldNX * ldSide, ldNY * ldSide, ldNZ * ldSide));
				}

				lOResult.Add(lOCorners);
			}

			return lOResult;
		}

		/// <summary>
		/// THE SIDE EACH FACE IS SEEN FROM (per user 2026-11-22, the bridges of the second game's Void: in the original the
		/// inner sides of a bridge's side walls are see-through, ours were drawn): a face lying in the plane of its innermost
		/// back-face test (the rule of FaceCorners) is drawn by the original only from the side that test shows. Per face the
		/// unit normal towards that side in the model's units (X, Y, Z with Z the height), or null for a face drawn from both
		/// sides (no test, or a test guarding faces that do not lie in its plane).
		/// </summary>
		public static List<UWVector3?> FaceFronts(UW3DModel pOModel)
		{
			List<UWVector3?> lOResult = new List<UWVector3?>(pOModel.Faces.Count);

			foreach (UW3DModelFace lOFace in pOModel.Faces)
			{
				UWVector3[] lOCorners = new UWVector3[lOFace.VertexIndices.Count];

				for (int liAt = 0; liAt < lOCorners.Length; liAt++)
					lOCorners[liAt] = pOModel.Vertices[lOFace.VertexIndices[liAt]];

				UW3DModelSideTest lOTest = fInnermostPlane(lOFace);

				if (lOTest != null && fPlane(lOTest, out double ldNX, out double ldNY, out double ldNZ, out double ldD)
					&& fAllNear(lOCorners, ldNX, ldNY, ldNZ, ldD))
				{
					double ldSide = lOTest.Else ? -1 : 1;

					lOResult.Add(new UWVector3((float)(ldNX * ldSide), (float)(ldNY * ldSide), (float)(ldNZ * ldSide)));
				}
				else
					lOResult.Add(null);
			}

			return lOResult;
		}

		/// <summary>How close to an edge a corner of another face counts as lying on it: a model unit (a tile is 256).</summary>
		public const float EdgeTolerance = 1f / 256f;

		/// <summary>
		/// THE T-JUNCTIONS CLOSED (per user 2026-11-22, cracks in the shrine and a boulder): where a corner of one face lies
		/// on an edge of another, that edge gets the corner too, so both faces meet in the same points (the shrine's loop on
		/// 40-44 at 42 and its crossbar on 6-75 at 8, the large boulder on 7-18 at 35). The original's rasterizer fills a
		/// polygon from its outline and shows no crack there; ours splits it into triangles, which can leave one. Faces with a
		/// corner raised to the ceiling at runtime are left as they are. Returns how many corners were put in.
		/// </summary>
		public static int CloseTJunctions(UW3DModel pOModel)
		{
			if (pOModel == null)
				return 0;

			int liAdded = 0;

			foreach (UW3DModelFace lOFace in pOModel.Faces)
			{
				if (lOFace.VertexIndices.Exists(piAt => pOModel.CeilingVertexIndices.Contains(piAt)))
					continue;

				for (int liEdge = 0; liEdge < lOFace.VertexIndices.Count; liEdge++)
				{
					int liA = lOFace.VertexIndices[liEdge];
					int liB = lOFace.VertexIndices[(liEdge + 1) % lOFace.VertexIndices.Count];
					UWVector3 lA = pOModel.Vertices[liA];
					UWVector3 lB = pOModel.Vertices[liB];
					double ldDX = lB.X - lA.X, ldDY = lB.Y - lA.Y, ldDZ = lB.Z - lA.Z;
					double ldLength2 = ldDX * ldDX + ldDY * ldDY + ldDZ * ldDZ;

					if (ldLength2 < 1e-12)
						continue;

					int liOn = -1;
					double ldOnT = 2;

					for (int liV = 0; liV < pOModel.Vertices.Count; liV++)
					{
						if (lOFace.VertexIndices.Contains(liV) || pOModel.CeilingVertexIndices.Contains(liV))
							continue;

						UWVector3 lP = pOModel.Vertices[liV];
						double ldT = ((lP.X - lA.X) * ldDX + (lP.Y - lA.Y) * ldDY + (lP.Z - lA.Z) * ldDZ) / ldLength2;

						if (ldT <= 0.001 || ldT >= 0.999 || ldT >= ldOnT)
							continue;

						double ldQX = lA.X + ldT * ldDX - lP.X, ldQY = lA.Y + ldT * ldDY - lP.Y, ldQZ = lA.Z + ldT * ldDZ - lP.Z;

						if (ldQX * ldQX + ldQY * ldQY + ldQZ * ldQZ > EdgeTolerance * EdgeTolerance)
							continue;

						// Only a corner another face uses: a vertex no face names is no junction.
						if (!pOModel.Faces.Exists(pOOther => pOOther != lOFace && pOOther.VertexIndices.Contains(liV)))
							continue;

						liOn = liV;
						ldOnT = ldT;
					}

					if (liOn < 0)
						continue;

					// The nearest such corner goes in after A; the edge from it to B is looked at in the next pass.
					lOFace.VertexIndices.Insert(liEdge + 1, liOn);

					if (lOFace.Uvs != null && lOFace.Uvs.Count > liEdge)
					{
						UWVector2 lUA = lOFace.Uvs[liEdge];
						UWVector2 lUB = lOFace.Uvs[(liEdge + 1) % lOFace.Uvs.Count];

						// A list of the face's own (the parser may hand one list to several faces).
						lOFace.Uvs = new List<UWVector2>(lOFace.Uvs);
						lOFace.Uvs.Insert(liEdge + 1, new UWVector2((float)(lUA.X + (lUB.X - lUA.X) * ldOnT), (float)(lUA.Y + (lUB.Y - lUA.Y) * ldOnT)));
					}

					liAdded++;
				}
			}

			return liAdded;
		}

		/// <summary>A laid face with its own plane: Newell's normal of the laid corners, turned to the test's side.</summary>
		private static LaidFace fLay(UWVector3[] pOCorners, UWVector3[] pOOriginal, double pdNX, double pdNY, double pdNZ)
		{
			double ldX = 0, ldY = 0, ldZ = 0, ldCX = 0, ldCY = 0, ldCZ = 0;
			int liCount = pOCorners.Length;

			for (int liAt = 0; liAt < liCount; liAt++)
			{
				UWVector3 lA = pOCorners[liAt];
				UWVector3 lB = pOCorners[(liAt + 1) % liCount];

				ldX += (lA.Y - lB.Y) * (lA.Z + lB.Z);
				ldY += (lA.Z - lB.Z) * (lA.X + lB.X);
				ldZ += (lA.X - lB.X) * (lA.Y + lB.Y);
				ldCX += lA.X;
				ldCY += lA.Y;
				ldCZ += lA.Z;
			}

			double ldLength = Math.Sqrt(ldX * ldX + ldY * ldY + ldZ * ldZ);

			if (ldLength < 1e-12)
			{
				ldX = pdNX;
				ldY = pdNY;
				ldZ = pdNZ;
				ldLength = 1;
			}

			ldX /= ldLength;
			ldY /= ldLength;
			ldZ /= ldLength;

			if (ldX * pdNX + ldY * pdNY + ldZ * pdNZ < 0)
			{
				ldX = -ldX;
				ldY = -ldY;
				ldZ = -ldZ;
			}

			double ldOD = -(ldX * ldCX + ldY * ldCY + ldZ * ldCZ) / liCount;
			double ldAbove = 0;

			foreach (UWVector3 lV in pOCorners)
				ldAbove = Math.Max(ldAbove, ldX * lV.X + ldY * lV.Y + ldZ * lV.Z + ldOD);

			return new LaidFace
			{
				Corners = pOCorners, Original = pOOriginal, NX = pdNX, NY = pdNY, NZ = pdNZ,
				ONX = ldX, ONY = ldY, ONZ = ldZ, OD = ldOD, Above = ldAbove
			};
		}

		/// <summary>Whether a face (its test's normal towards the shown side) lies on nearly the same surface as an earlier:
		/// normals within ParallelCosine, every corner within PlaneTolerance of the earlier face's own plane.</summary>
		private static bool fSameSurface(UWVector3[] pOCorners, double pdNX, double pdNY, double pdNZ, LaidFace pOEarlier)
		{
			if (pdNX * pOEarlier.NX + pdNY * pOEarlier.NY + pdNZ * pOEarlier.NZ < ParallelCosine)
				return false;

			foreach (UWVector3 lV in pOCorners)
				if (Math.Abs(pOEarlier.ONX * lV.X + pOEarlier.ONY * lV.Y + pOEarlier.ONZ * lV.Z + pOEarlier.OD) > PlaneTolerance)
					return false;

			return true;
		}

		/// <summary>
		/// Whether two faces of one surface overlap, seen along its normal - not merely touch: a point well inside the one
		/// lying well inside the other, either way round (fAnySampleInside). Two triangles of a boulder's facet share an edge
		/// and cover nothing; a band on the chest's wood lies inside it.
		/// </summary>
		private static bool fOverlap(UWVector3[] pOA, UWVector3[] pOB, double pdNX, double pdNY, double pdNZ)
		{
			// Two axes across the normal.
			double ldUX, ldUY, ldUZ;

			if (Math.Abs(pdNX) < 0.9)
			{
				ldUX = 0;
				ldUY = pdNZ;
				ldUZ = -pdNY;
			}
			else
			{
				ldUX = -pdNZ;
				ldUY = 0;
				ldUZ = pdNX;
			}

			double ldU = Math.Sqrt(ldUX * ldUX + ldUY * ldUY + ldUZ * ldUZ);

			ldUX /= ldU;
			ldUY /= ldU;
			ldUZ /= ldU;

			double ldVX = pdNY * ldUZ - pdNZ * ldUY, ldVY = pdNZ * ldUX - pdNX * ldUZ, ldVZ = pdNX * ldUY - pdNY * ldUX;

			double[] lA = fFlat(pOA, ldUX, ldUY, ldUZ, ldVX, ldVY, ldVZ);
			double[] lB = fFlat(pOB, ldUX, ldUY, ldUZ, ldVX, ldVY, ldVZ);

			return fAnySampleInside(lA, lB) || fAnySampleInside(lB, lA);
		}

		private static double[] fFlat(UWVector3[] pOCorners, double pdUX, double pdUY, double pdUZ, double pdVX, double pdVY, double pdVZ)
		{
			double[] lResult = new double[pOCorners.Length * 2];

			for (int liAt = 0; liAt < pOCorners.Length; liAt++)
			{
				UWVector3 lV = pOCorners[liAt];

				lResult[liAt * 2] = pdUX * lV.X + pdUY * lV.Y + pdUZ * lV.Z;
				lResult[liAt * 2 + 1] = pdVX * lV.X + pdVY * lV.Y + pdVZ * lV.Z;
			}

			return lResult;
		}

		/// <summary>Samples per axis over a face's bounding box for the overlap test.</summary>
		private const int SampleGrid = 16;

		/// <summary>How far inside both outlines an overlap sample must lie: a quarter model unit - the chest's band along the
		/// top of its end is only two units tall.</summary>
		public const float OverlapMargin = 0.25f / 256f;

		/// <summary>
		/// Whether a point well inside the one face lies well inside the other: a grid over the face's bounding box, each
		/// point inside the face itself and inside the other, at least OverlapMargin from both outlines. A grid, not a few
		/// points by the corners (per user 2026-11-22: the chest's end panels lie almost on the end's wood and share most of
		/// its edges - points next to the corners all fell by a shared edge, and the panels were taken for neighbours).
		/// </summary>
		private static bool fAnySampleInside(double[] pPolygon, double[] pOther)
		{
			int liCount = pPolygon.Length / 2;
			double ldMinX = double.MaxValue, ldMinY = double.MaxValue, ldMaxX = double.MinValue, ldMaxY = double.MinValue;

			for (int liAt = 0; liAt < liCount; liAt++)
			{
				ldMinX = Math.Min(ldMinX, pPolygon[liAt * 2]);
				ldMaxX = Math.Max(ldMaxX, pPolygon[liAt * 2]);
				ldMinY = Math.Min(ldMinY, pPolygon[liAt * 2 + 1]);
				ldMaxY = Math.Max(ldMaxY, pPolygon[liAt * 2 + 1]);
			}

			for (int liX = 0; liX < SampleGrid; liX++)
			{
				double ldX = ldMinX + (ldMaxX - ldMinX) * (liX + 0.5) / SampleGrid;

				for (int liY = 0; liY < SampleGrid; liY++)
				{
					double ldY = ldMinY + (ldMaxY - ldMinY) * (liY + 0.5) / SampleGrid;

					if (fInside(ldX, ldY, pPolygon) && fInside(ldX, ldY, pOther))
						return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Even-odd point in polygon, and at least OverlapMargin away from each of its edges (per user 2026-11-22: the
		/// shrine's middle piece under the loop shares two edges with the loop's ring, a sample next to them fell a hair
		/// inside the ring, and the piece was lifted - a bright line along its edges).
		/// </summary>
		private static bool fInside(double pdX, double pdY, double[] pPolygon)
		{
			int liCount = pPolygon.Length / 2;
			bool lbInside = false;

			for (int liAt = 0, liPrev = liCount - 1; liAt < liCount; liPrev = liAt++)
			{
				double ldXA = pPolygon[liAt * 2], ldYA = pPolygon[liAt * 2 + 1];
				double ldXB = pPolygon[liPrev * 2], ldYB = pPolygon[liPrev * 2 + 1];

				if (fEdgeDistance(pdX, pdY, ldXA, ldYA, ldXB, ldYB) < OverlapMargin)
					return false;

				if ((ldYA > pdY) != (ldYB > pdY) && pdX < (ldXB - ldXA) * (pdY - ldYA) / (ldYB - ldYA) + ldXA)
					lbInside = !lbInside;
			}

			return lbInside;
		}

		private static double fEdgeDistance(double pdX, double pdY, double pdXA, double pdYA, double pdXB, double pdYB)
		{
			double ldDX = pdXB - pdXA, ldDY = pdYB - pdYA;
			double ldLength2 = ldDX * ldDX + ldDY * ldDY;
			double ldT = ldLength2 < 1e-18 ? 0 : Math.Max(0, Math.Min(1, ((pdX - pdXA) * ldDX + (pdY - pdYA) * ldDY) / ldLength2));
			double ldQX = pdXA + ldT * ldDX - pdX, ldQY = pdYA + ldT * ldDY - pdY;

			return Math.Sqrt(ldQX * ldQX + ldQY * ldQY);
		}

		/// <summary>A face over pOEarlier comes to lie at least RankOffset in front of that face's own plane, corner by
		/// corner along its normal.</summary>
		private static void fPaintOver(UWVector3[] pOCorners, LaidFace pOEarlier)
		{
			for (int liAt = 0; liAt < pOCorners.Length; liAt++)
			{
				UWVector3 lV = pOCorners[liAt];
				// In front of the earlier face's frontmost corner, not only its mean plane (per user 2026-11-26, UW2's bed: the
				// side of its cover, painted after the grey sheet's side, let a triangle of the sheet show through - the sheet's
				// side of seven corners stands up to a model unit off its mean plane).
				double ldShort = RankOffset + pOEarlier.Above - (pOEarlier.ONX * lV.X + pOEarlier.ONY * lV.Y + pOEarlier.ONZ * lV.Z + pOEarlier.OD);

				if (ldShort > 0)
					pOCorners[liAt] = new UWVector3((float)(lV.X + pOEarlier.ONX * ldShort), (float)(lV.Y + pOEarlier.ONY * ldShort), (float)(lV.Z + pOEarlier.ONZ * ldShort));
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
