using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Patchwork
{
	/// <summary>
	/// ScriptEvents are pseudo-monobs in that the same global events are dispatched there.
	/// 
	/// Using overrides has the advantage of intellisense/type checking in IDE, as well
	/// as far less overhead [1] - only a single, dynamic IL built GO Component is present
	/// dispatching everything to individual scripts without having unity cross icall boundary
	/// for each individually every frame.
	///
	/// [1] http://blog.theknightsofunity.com/monobehavior-calls-optimization/
	/// </summary>
	public class ScriptEvents
	{
		/// <summary>
		/// Paused
		/// </summary>
		/// <param name="pauseStatus"></param>
		public virtual void OnApplicationPause(bool pauseStatus) { }
		/// <summary>
		/// In focus
		/// </summary>
		/// <param name="focusStatus"></param>
		public virtual void OnApplicationFocus(bool focusStatus) { }
		/// <summary>
		/// On level change (not scene!)
		/// </summary>
		/// <param name="level"></param>
		public virtual void OnLevelWasLoaded(int level) { }

		/// <summary>
		/// Called on exit
		/// </summary>
		public virtual void OnApplicationQuit() { }
		/// <summary>
		/// Early init - called right during script initialization
		/// </summary>
		public virtual void Awake() { }
		/// <summary>
		/// This function is called every fixed (vsync) framerate frame
		/// </summary>
		public virtual void FixedUpdate() { }
		/// <summary>
		/// LateUpdate is called every frame
		/// </summary>
		public virtual void LateUpdate() { }
		/// <summary>
		/// Called when the script is destroyed (for reload)
		/// </summary>
		public virtual void OnDestroy() { }
		/// <summary>
		/// Called when the script is re-enabled
		/// </summary>
		public virtual void OnEnable() { }
		/// <summary>
		/// Called when the script is disabled
		/// </summary>
		public virtual void OnDisable() { }
		/// <summary>
		/// Called to handle IMGUI events
		/// </summary>
		public virtual void OnGUI() { }
		/// <summary>
		/// Normal init - called after gameobject is properly initialized
		/// </summary>
		public virtual void Start() { }
		/// <summary>
		/// Update is called every frame
		/// </summary>
		public virtual void Update() { }

		/// <summary>
		/// Called during scene or level change
		/// </summary>
		/// <param name="name"></param>
		/// <param name="subname"></param>
		/// <returns>true if the scene change should be cancelled</returns>
		public virtual bool OnScene(string name, string subname) { return false; }

		/// <summary>
		/// Called when card is being loaded
		/// </summary>
		/// <param name="f">card file instance</param>
		/// <param name="bh">to access raw data sections</param>
		/// <param name="nopng">nopng, typically means save/load</param>
		/// <param name="nostatus"></param>
		public virtual void OnCardLoad(ChaFile f, BlockHeader bh, bool nopng, bool nostatus) { }
		/// <summary>
		/// Called when card is being saved
		/// </summary>
		/// <param name="f">card file instance</param>
		/// <param name="w">raw stream</param>
		/// <param name="blocks">blocks in ChaFileBase format</param>
		/// <param name="nopng">true if savefile</param>
		public virtual void OnCardSave(ChaFile f, BinaryWriter w, List<object> blocks, bool nopng) { }

		/// <summary>
		/// A category item is being added.
		/// </summary>
		/// <param name="lib">Item entry, can be mutated</param>
		public virtual void OnSetListInfo(ListInfoBase lib) { }

		/// <summary>
		/// A category item is being queried.
		/// </summary>
		/// <param name="lib">Item entry, can be mutated</param>
		public virtual void OnGetListInfo(ref ListInfoBase lib, int cat, int id) { }

		/// <summary>
		/// Gets executed "occasionaly", roughly every second.
		/// </summary>
		/// <param name="o"></param>
		public virtual void Occasion() { }

		/// <summary>
		/// Print stringified object.
		/// </summary>
		/// <param name="o"></param>
		public static void print(object o)
		{
			Script.print(o);
		}

		/// <summary>
		/// Pretty print an object.
		/// </summary>
		/// <param name="o"></param>
		public static void pp(object o)
		{

			Script.pp(o);
		}

		/// <summary>
		/// Called when picking clothes in editor (for current coordinate). Needed for mod interop.
		/// </summary>
		/// <param name="ch"></param>
		/// <param name="cat"></param>
		/// <param name="ids"></param>
		/// <returns></returns>
		public virtual void OnSetClothes(ChaControl ch, int cat, int[] ids) { }
	}
}
