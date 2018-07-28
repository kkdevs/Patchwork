//@INFO: Additional floor, tit and hands db/colliders (eroigame.net)
//@VER: 1

// Physics tweaks as described in http://eroigame.net/archives/1387
// Done programatically, so it works on any skeleton and can adapt to tit sizes

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static ChaFileDefine;

public class FixColliders : ScriptEvents {
	public FindAssist fa;
	public override void OnLoadFBX(ChaControl ctrl, ref GameObject go, string ab = null, string ass = null, ListInfoBase lib = null)
	{
		if (go == null) return;
		try
		{
			if (go.name == "p_cf_body_bone")
				TweakBody(ctrl, go);
			
			else if (ass.StartsWith("p_o_bot_skirt"))
				TweakSkirt(ctrl, go);
			// XXX: maybe other clothing pieces with db's too?
			//print(ass);
		}
		catch (System.Exception ex)
		{
			print(ex);
		}
	}
	public void TweakSkirt(ChaControl ctrl, GameObject go)
	{
		var radius = new float[] { 0.03f,0.045f,0.02f,0.045f,0.03f,0.045f,0.02f,0.045f};
		foreach (var db in go.GetComponentsInChildren<DynamicBone>())
		{
			int idx = int.Parse(db.m_Root.name.Split('_')[3]);
			db.m_Radius = radius[idx];
			db.m_FreezeAxis = DynamicBone.FreezeAxis.X;
			if (idx == 0)
			{
				var keys = db.m_RadiusDistrib.keys;
				keys[keys.Length-1].value = 1.6f;
				db.m_RadiusDistrib.keys = keys;
			}
			db.SetupParticles();
		}
	}

	public void TweakBody(ChaControl ctrl, GameObject go) {
		var bc = new List<DynamicBoneCollider>();
		fa = new FindAssist(go.transform);

		// floor pseudo-collider
		bc.Add(AddCollider(go, "cf_j_root", sz: 100, t: new Vector3(0,-10.01f,-0.01f)));

		float titsize = ctrl.chaFile.custom.body.shapeValueBody[(int)BodyShapeIdx.BustSize];
		//XXX: maybe skip for small tits in general?
		//if (ctrl.sex == 0) return;
		//if (titsize < 0.2f) return;

		// bind colliders to hands
		foreach (var n in "forearm02 arm02 hand".Split(' '))
		{
			bc.Add(AddCollider(go, "cf_s_" + n + "_L", sx: 0.35f, sy: 0.35f, sz: 0.3f));
			bc.Add(AddCollider(go, "cf_s_" + n + "_R", sx: 0.35f, sy: 0.35f, sz: 0.3f));
		}

		// large tits need special case as the standard hitbox morph doesn't like shape values above 1
		var cowfactor = System.Math.Max(1f, titsize);

		// tell the tits that hands collide it
		foreach (var tit in go.GetComponentsInChildren<DynamicBone_Ver02>(true))
		{
			if ((tit.Comment != "右胸") && (tit.Comment != "左胸"))
				continue;
			// register the colliders if not already there
			foreach (var c in bc)
				if (c != null && !tit.Colliders.Contains(c))
					tit.Colliders.Add(c);
			// expand the collision radius for the first two dynbones
			foreach (var pat in tit.Patterns) {
				pat.Params[0].CollisionRadius = 0.08f * cowfactor;
				pat.Params[1].CollisionRadius = 0.06f * cowfactor;
			}
			tit.Reset();
		}
		fa = null;
	}
	public DynamicBoneCollider AddCollider(GameObject go, string bone, float sx = 1, float sy = 1, float sz = 1, Vector3 r = new Vector3(), Vector3 t = new Vector3())
	{
		var hitbone = bone + "_hit";
		var bo = fa.GetObjectFromName(hitbone);
		if (bo != null)
			return null;

		// some collider is already in there, so just keep that
		var parent = fa.GetObjectFromName(bone);
		if (parent == null)
			return null;

		// build the collider
		var nb = new GameObject(hitbone);
		var col = nb.AddComponent<DynamicBoneCollider>();
		col.m_Radius = 0.1f;
		col.m_Direction = DynamicBoneCollider.Direction.Y;
		nb.transform.SetParent(parent.transform, false);
		nb.transform.localScale = new Vector3(sx, sy, sz);
		nb.transform.localEulerAngles = r;
		nb.transform.localPosition = t;
		nb.layer = 12;
		return col;
	}
}

