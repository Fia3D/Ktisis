using Dalamud.Logging;

using Ktisis.Structs.Actor.State;

namespace Ktisis.Events {
	public static class EventManager {
		public delegate void GPoseChange(ActorGposeState state);
		public static GPoseChange? OnGPoseChange = null;

		public static void FireOnGposeChangeEvent(ActorGposeState state) {
			PluginLog.Debug($"FireOnGposeChangeEvent {state}");
			OnGPoseChange?.Invoke(state);
		}
	}
}