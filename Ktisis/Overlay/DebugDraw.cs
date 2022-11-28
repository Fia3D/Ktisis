using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;

using ImGuiNET;

using Ktisis.Helpers;

namespace Ktisis.Overlay {
	public static class DebugDraw {
		private static readonly List<(Vector3, float)> ToDraw1 = new();
		private static readonly List<(Vector3, Quaternion, uint color, float)> ToDraw2 = new();
		private static readonly List<(Vector3, Vector3, uint color, float)> ToDraw3 = new();

		private unsafe static Vector2 WorldToScreen(Vector3 world) {
			if (OverlayWindow.WorldMatrix == null)
				return new Vector2(-1, -1);
			OverlayWindow.WorldMatrix->WorldToScreen(world, out var screen);
			return screen;
		}

		private static ImGuiIOPtr GetIo() => ImGui.GetIO();

		private static ImDrawListPtr Drawer() => ImGui.GetWindowDrawList();

		
		public static void Add(Vector3 pos, float length = 1.0f) {
			ToDraw1.Add((pos, length));
		}
		public static void Add(Vector3 pos, Quaternion rot, uint color = 0xFF00FFFF, float thickeness = 1.0f) {
			ToDraw2.Add((pos, rot, color, thickeness));
		}

		public static void Add(Vector3 pos1, Vector3 pos2, uint color = 0xFF00FFFF, float thickeness = 1.0f) {
			ToDraw3.Add((pos1, pos2, color, thickeness));
		}
		
		public static void Draw() {
			_debugDrawing();
			ToDraw1.Clear();
			ToDraw2.Clear();
			ToDraw3.Clear();
		}

		private static void _debugDrawing() {
			foreach (var axis in ToDraw1) {
				var posScreen1 = WorldToScreen(axis.Item1);
				Drawer().AddLine(posScreen1, WorldToScreen(axis.Item1 + Vector3.UnitX), 0xFF000000, axis.Item2*2);
				Drawer().AddLine(posScreen1, WorldToScreen(axis.Item1 + Vector3.UnitY), 0xFF000000, axis.Item2*2);
				Drawer().AddLine(posScreen1, WorldToScreen(axis.Item1 + Vector3.UnitZ), 0xFF000000, axis.Item2*2);
				Drawer().AddLine(posScreen1, WorldToScreen(axis.Item1 + Vector3.UnitX), 0xFF0000FF, axis.Item2);
				Drawer().AddLine(posScreen1, WorldToScreen(axis.Item1 + Vector3.UnitY), 0xFF00FF00, axis.Item2);
				Drawer().AddLine(posScreen1, WorldToScreen(axis.Item1 + Vector3.UnitZ), 0xFFFF0000, axis.Item2);
			}
			foreach (var axis in ToDraw2) {
				var posScreen1 = WorldToScreen(axis.Item1);
				Drawer().AddLine(posScreen1, WorldToScreen(axis.Item1 + Vector3.Normalize(MathHelpers.ToEuler(axis.Item2))), 0xFF000000, axis.Item4*2);
				Drawer().AddLine(posScreen1, WorldToScreen(axis.Item1 + Vector3.Normalize(MathHelpers.ToEuler(axis.Item2))), axis.Item3, axis.Item4);
			}
			foreach (var axis in ToDraw3) {
				var posScreen1 = WorldToScreen(axis.Item1);
				var posScreen2 = WorldToScreen(axis.Item2);
				Drawer().AddLine(posScreen1, posScreen2, 0xFF000000, axis.Item4*2);
				Drawer().AddLine(posScreen1, posScreen2, axis.Item3, axis.Item4);
			}
		}
		private static Vector3 RotateVector(Vector3 vector, Vector3 axis, float angle) {
			Vector3 vxp = Vector3.Cross(axis, vector);
			Vector3 vxvxp = Vector3.Cross(axis, vxp);
			return vector + MathF.Sin(angle) * vxp + (1 - MathF.Cos(angle)) * vxvxp;
		}

		private static Vector3 RotateVectorAboutPoint(Vector3 vector, Vector3 pivot, Vector3 axis, float angle) {
			return pivot + RotateVector(vector - pivot, axis, angle);
		}
	}
}