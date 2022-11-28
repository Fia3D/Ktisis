using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

using Ktisis.Interop;
using Ktisis.Data.Excel;
using Ktisis.Interop.Hooks;

using Dalamud.Logging;

using FFXIVClientStructs.Havok;

using ImGuiNET;

using ImGuizmoNET;

using Ktisis.Overlay;
using Ktisis.Structs.Bones;

namespace Ktisis.Structs.Actor {
	[StructLayout(LayoutKind.Explicit, Size = 0x84A)]
	public struct Actor {
		[FieldOffset(0)] public GameObject GameObject;

		[FieldOffset(0x88)] public byte ObjectID;

		[FieldOffset(0xF0)] public unsafe ActorModel* Model;
		[FieldOffset(0x104)] public RenderMode RenderMode;
		[FieldOffset(0x1B4)] public int ModelId;

		[FieldOffset(0x6E0)] public Weapon MainHand;
		[FieldOffset(0x748)] public Weapon OffHand;
		[FieldOffset(0x818)] public Equipment Equipment;
		[FieldOffset(0x840)] public Customize Customize;

		[FieldOffset(0xC20)] public ActorGaze Gaze;

		[FieldOffset(0x1A68)] public byte TargetObjectID;
		[FieldOffset(0x1A6C)] public byte TargetMode;

		public unsafe string? Name => Marshal.PtrToStringAnsi((IntPtr)GameObject.GetName());

		public string GetNameOr(string fallback) => ((ObjectKind)GameObject.ObjectKind == ObjectKind.Pc && !Ktisis.Configuration.DisplayCharName) || string.IsNullOrEmpty(Name) ? fallback : Name;
		public string GetNameOrId() => GetNameOr("Actor #" + ObjectID);

		public unsafe IntPtr GetAddress() {
			fixed (Actor* self = &this) return (IntPtr)self;
		}

		// Targeting

		public unsafe void TargetActor(Actor* actor) {
			TargetObjectID = actor->ObjectID;
			TargetMode = 2;
		}

		public unsafe void LookAt(Gaze* tar, GazeControl bodyPart) {
			if (Methods.ActorLookAt == null) return;
			if (!PoseHooks.PosingEnabled)
				fixed (ActorGaze* gaze = &Gaze) {
					Methods.ActorLookAt(
						gaze,
						tar,
						bodyPart,
						IntPtr.Zero
					);
				}
			else {
				if (Model == null)
					return;
				var skeleton = Model->Skeleton;
				if (skeleton == null)
					return;
				var partialSkeleton = Model->Skeleton->PartialSkeletons;
				if (partialSkeleton == null)
					return;
				var headName = "j_kao";
				Bone? headBone = null;
				for (var p = 0; p < skeleton->PartialSkeletonCount; p++) {
					var partial = skeleton->PartialSkeletons[p];
					var pose = partial.GetHavokPose(0);
					if (pose == null)
						continue;
					var poseSkeleton = pose->Skeleton;
					if (poseSkeleton == null)
						continue;
					// Find head
					for (var i = 1; i < poseSkeleton->Bones.Length; i++) {
						var tempBone = skeleton->GetBone(p, i);
						if (tempBone.HkaBone.Name.String.Equals(headName))
							headBone = tempBone;
					}
				}

				if (headBone == null)
					return;

				if (tar == null)
					return;

				var lookAtRotation = LookAt(headBone.GetWorldPos(Model), tar->Pos, -Vector3.UnitX, Vector3.UnitZ);
				var initialRot = headBone.Transform.Rotation.ToQuat();
				var initialPos = headBone.Transform.Translation.ToVector3();
				var transform = headBone.Transform;
				transform.Rotation = lookAtRotation.ToHavok();
				headBone.Transform = transform;
				
				Skeleton.PropagateChildren(headBone, &transform, initialPos, initialRot);
			}
		}

		private Quaternion LookAt(Vector3 sourcePoint, Vector3 destPoint, Vector3 front, Vector3 up)
		{
			Vector3 toVector = Vector3.Normalize(destPoint - sourcePoint);

			// LookAt Axis
			Vector3 rotAxis = Vector3.Normalize(Vector3.Cross(front, toVector));
			
			if (rotAxis.LengthSquared() == 0)
				rotAxis = up;
			
			// Angle around Axis
			var dot = Vector3.Dot(front, toVector);
			var ang = (float)Math.Acos(dot);

			// Axis Angle to Quaternion
			return AngleAxis(rotAxis, ang);
		}
		
		Quaternion AngleAxis(Vector3 axis, float angle) {
			var s = Math.Sin(angle / 2);
			var u = Vector3.Normalize(axis);
			return new Quaternion((float)Math.Cos(angle / 2), (float)(u.X * s), (float)(u.Y * s), (float)(u.Z * s));
		}

		// Change equipment - no redraw method

		public unsafe void Equip(EquipIndex index, ItemEquip item) {
			if (Methods.ActorChangeEquip == null) return;
			Methods.ActorChangeEquip(GetAddress() + 0x6D0, index, item);
		}
		public void Equip(List<(EquipSlot, object)> items) {
			foreach ((EquipSlot slot, object item) in items)
				if (item is ItemEquip equip)
					Equip(Interface.Windows.ActorEdit.EditEquip.SlotToIndex(slot), equip);
				else if (item is WeaponEquip wep)
					Equip((int)slot, wep);
		}

		public void Equip(int slot, WeaponEquip item) {
			if (Methods.ActorChangeWeapon == null) return;
			Methods.ActorChangeWeapon(GetAddress() + 0x6D0, slot, item, 0, 1, 0, 0);
		}

		// Change customize - no redraw method

		public unsafe bool UpdateCustomize() {
			fixed (Customize* custom = &Customize)
				return ((Human*)Model)->UpdateDrawData((byte*)custom, true);
		}

		// Actor redraw

		public unsafe void Redraw(bool faceHack = false) {
			faceHack &= GameObject.ObjectKind == (byte)ObjectKind.Pc;
			GameObject.DisableDraw();
			if (faceHack) GameObject.ObjectKind = (byte)ObjectKind.BattleNpc;
			GameObject.EnableDraw();
			if (faceHack) GameObject.ObjectKind = (byte)ObjectKind.Pc;
		}
	}

	public enum RenderMode : uint {
		Draw = 0,
		Unload = 2,
		Load = 4
	}
}