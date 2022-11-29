using System.Numerics;

using ImGuiNET;

using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Game.ClientState.Objects.Types;

using Ktisis.Util;
using Ktisis.Overlay;
using Ktisis.Localization;
using Ktisis.Structs;
using Ktisis.Structs.Actor;
using Ktisis.Interop.Hooks;
using Ktisis.Interface.Components;
using Ktisis.Interface.Windows.ActorEdit;

namespace Ktisis.Interface.Windows.Workspace {
	public static class Workspace {
		public static bool Visible = false;

		public static Vector4 ColGreen = new Vector4(0, 255, 0, 255);
		public static Vector4 ColRed = new Vector4(255, 0, 0, 255);

		public static TransformTable Transform = new();

		// Toggle visibility

		public static void Show() => Visible = true;
		public static void OnEnterGposeToggle(Structs.Actor.State.ActorGposeState gposeState) {
			if (Ktisis.Configuration.OpenKtisisMethod == OpenKtisisMethod.OnEnterGpose)
				Visible = gposeState == Structs.Actor.State.ActorGposeState.ON;
		}

		public static float PanelHeight => ImGui.GetTextLineHeight() * 2 + ImGui.GetStyle().ItemSpacing.Y + ImGui.GetStyle().FramePadding.Y;

		// Draw window

		public static void Draw() {
			if (!Visible)
				return;

			var gposeOn = Ktisis.IsInGPose;

			var size = new Vector2(-1, -1);
			ImGui.SetNextWindowSize(size, ImGuiCond.FirstUseEver);

			ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10, 10));

			if (ImGui.Begin("Ktisis (Alpha)", ref Visible, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize)) {

				ControlButtons.PlaceAndRenderSettings();

				ImGui.BeginGroup();
				ImGui.AlignTextToFramePadding();

				ImGui.TextColored(
					gposeOn ? ColGreen : ColRed,
					gposeOn ? "GPose Enabled" : "GPose Disabled"
				);

				ImGui.SameLine();

				// Pose switch
				ControlButtons.DrawPoseSwitch();

				var target = Ktisis.GPoseTarget;
				if (target == null) return;

				// Selection info
				SelectInfo(target);

				// Actor control

				ImGui.Separator();

				if (ImGui.BeginTabBar(Locale.GetString("Workspace"))) {
					if (ImGui.BeginTabItem(Locale.GetString("Actor")))
						ActorTab(target);
					/*if (ImGui.BeginTabItem(Locale.GetString("Scene")))
						SceneTab();*/
					if (ImGui.BeginTabItem(Locale.GetString("Pose")))
						PoseTab(target);
				}
			}

			ImGui.PopStyleVar();
			ImGui.End();
		}

		// Actor tab (Real)

		private unsafe static void ActorTab(GameObject target) {
			var cfg = Ktisis.Configuration;

			if (target == null) return;

			var actor = (Actor*)target.Address;
			if (actor->Model == null) return;

			// Actor details

			ImGui.Spacing();

			// Customize button
			if (ImGuiComponents.IconButton(FontAwesomeIcon.UserEdit)) {
				if (EditActor.Visible)
					EditActor.Hide();
				else
					EditActor.Show();
			}
			ImGui.SameLine();
			ImGui.Text("Edit actor's appearance");

			ImGui.Spacing();

			// Actor list
			ActorsList.Draw();

			// Animation control
			AnimationControls.Draw(target);

			// Gaze control
			if (ImGui.CollapsingHeader("Gaze Control")) {
				EditGaze.Draw(actor);
			}

			ImGui.EndTabItem();
		}

		// Pose tab

		public static PoseContainer _TempPose = new();

		private unsafe static void PoseTab(GameObject target) {
			var cfg = Ktisis.Configuration;

			if (target == null) return;

			var actor = (Actor*)target.Address;
			if (actor->Model == null) return;

			// Extra Controls
			ControlButtons.DrawExtra();

			// Parenting

			var parent = cfg.EnableParenting;
			if (ImGui.Checkbox("Parenting", ref parent))
				cfg.EnableParenting = parent;

			// Transform table
			TransformTable(actor);

			ImGui.Spacing();

			// Bone categories
			if (ImGui.CollapsingHeader("Bone Categories")) {

				if (!Categories.DrawToggleList(cfg)) {
					ImGui.Text("No bone found.");
					ImGui.Text("Show Skeleton (");
					ImGui.SameLine();
					GuiHelpers.Icon(FontAwesomeIcon.EyeSlash);
					ImGui.SameLine();
					ImGui.Text(") to fill this.");
				}
			}

			// Bone tree
			BoneTree.Draw(actor);

			// Import & Export
			if (ImGui.CollapsingHeader("Import & Export")) {
				ImGui.Text("Transforms");
				var _ = false;
				ImGui.Checkbox("R", ref _);
				ImGui.SameLine();
				ImGui.Checkbox("P", ref _);
				ImGui.SameLine();
				ImGui.Checkbox("S", ref _);

				ImGui.Checkbox("Body", ref _);
				ImGui.SameLine();
				ImGui.Checkbox("Expression", ref _);

				ImGui.Spacing();

				ImGui.Button("Import");
				ImGui.SameLine();
				ImGui.Button("Export");

				ImGui.Spacing();
			}

			// Advanced
			if (ImGui.CollapsingHeader("Advanced")) {
				if (ImGui.Button("Set to Reference Pose")) {
					if (actor->Model != null && actor->Model->Skeleton != null) {
						var skele = actor->Model->Skeleton;
						for (var p = 0; p < skele->PartialSkeletonCount; p++) {
							var partial = skele->PartialSkeletons[p];
							var pose = partial.GetHavokPose(0);
							if (pose == null) continue;
							pose->SetToReferencePose();
							PoseHooks.SyncModelSpaceHook.Original(pose);
						}
					}
				}

				if (ImGui.Button("Store Pose") && actor->Model != null)
					_TempPose.Store(actor->Model->Skeleton);
				ImGui.SameLine();
				if (ImGui.Button("Apply Pose") && actor->Model != null)
					_TempPose.Apply(actor->Model->Skeleton);

				if (ImGui.Button("Sync Model Space (Debug)")) {
					if (actor->Model != null && actor->Model->Skeleton != null) {
						var skele = actor->Model->Skeleton;
						for (var p = 0; p < skele->PartialSkeletonCount; p++) {
							var partial = skele->PartialSkeletons[p];
							var pose = partial.GetHavokPose(0);
							if (pose == null) continue;
							PoseHooks.SyncModelSpaceHook.Original(pose);
						}
					}
				}
			}

			ImGui.EndTabItem();
		}

		// Transform Table actor and bone names display, actor related extra

		private static unsafe bool TransformTable(Actor* target) {
			var select = Skeleton.BoneSelect;
			var bone = Skeleton.GetSelectedBone();

			if (!select.Active) return Transform.Draw(target);
			if (bone == null) return false;

			return Transform.Draw(bone);
		}

		// Selection details

		private unsafe static void SelectInfo(GameObject target) {
			var actor = (Actor*)target.Address;

			var select = Skeleton.BoneSelect;
			var bone = Skeleton.GetSelectedBone();

			var frameSize = new Vector2(ImGui.GetContentRegionAvail().X, PanelHeight);
			ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(ImGui.GetStyle().FramePadding.X, ImGui.GetStyle().FramePadding.Y / 2));
			if (ImGui.BeginChildFrame(8, frameSize, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar)) {
				GameAnimationIndicator();

				ImGui.BeginGroup();

				// display target name
				ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (ImGui.GetStyle().FramePadding.Y / 2));
				ImGui.Text(actor->GetNameOrId());

				// display selected bone name
				ImGui.SetCursorPosY(ImGui.GetCursorPosY() - (ImGui.GetStyle().ItemSpacing.Y / 2) - (ImGui.GetStyle().FramePadding.Y / 2));
				if (select.Active && bone != null) {
					ImGui.Text($"{bone.LocaleName}");
				} else {
					ImGui.BeginDisabled();
					ImGui.Text("No bone selected");
					ImGui.EndDisabled();
				}

				ImGui.EndGroup();

				ImGui.EndChildFrame();
			}
			ImGui.PopStyleVar();
		}

		private static unsafe void GameAnimationIndicator() {
			var target = Ktisis.GPoseTarget;
			if (target == null) return;

			var isGamePlaybackRunning = PoseHooks.IsGamePlaybackRunning(target);
			var icon = isGamePlaybackRunning ? FontAwesomeIcon.Play : FontAwesomeIcon.Pause;

			var size = GuiHelpers.CalcIconSize(icon).X;

			ImGui.SameLine(size / 1.5f);

			ImGui.BeginGroup();

			ImGui.Dummy(new Vector2(size, size) / 2);

			GuiHelpers.Icon(icon);
			GuiHelpers.Tooltip(isGamePlaybackRunning ? "Game Animation is playing for this target." + (PoseHooks.PosingEnabled ? "\nPosing may reset periodically." : "") : "Game Animation is paused for this target." + (!PoseHooks.PosingEnabled ? "\nAnimation Control Can be used." : ""));

			ImGui.EndGroup();

			ImGui.SameLine(size * 2.5f);
		}
	}
}