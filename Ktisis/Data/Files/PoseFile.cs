﻿using System.Numerics;

using Ktisis.Structs;

namespace Ktisis.Data.Files {
	public class PoseFile : JsonFile {
		public string FileExtension = ".pose";
		public string TypeName = "Ktisis Pose";

		public Vector3? Position { get; set; }
		public Quaternion? Rotation { get; set; }
		public Vector3? Scale { get; set; }

		public PoseContainer? Bones { get; set; }
	}
}