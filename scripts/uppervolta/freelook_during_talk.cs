// upillu3352.zip[KK] GOLマクロ集(全員脱衣、衆人環視H、女の子回転、スタジオキャラ一括変更)v.1.0

using UnityEngine;

public class TalkFreeLook : MonoBehaviour
{
    public void Update()
    {
	    var charaDict = Manager.Character.Instance.dictEntryChara;
            
        if (charaDict == null || charaDict.Count == 0) return;
	        
	    ChaControl female = null;
	    for (int i = charaDict.Count - 1; i >= 0; i--)
	    {
	        if (charaDict[i].sex != 0 && charaDict[i].hiPoly && charaDict[i].transform.parent.name == "ActionScene")
	            female = charaDict[i];
	    }
	        
	    if(female == null) return;
	        
		var t = female.transform;
		var rot = t.rotation;
	        
	    if(Input.GetKey("right")||Input.GetKey("left")||Input.GetKey("up")||Input.GetKey("down"))
	    {
	        var go = GameObject.Find("ActionScene/ADVScene/Canvas_BackLog");
	        if(go != null)
	        {
	        	var comp=go.GetComponent<Canvas>();
	        	if(comp.enabled) return;
	        }
	        	
	        go = GameObject.Find("TalkScene");
	        if(go != null)
	        {
	        	var comp = go.GetComponent<UniRx.Triggers.ObservableUpdateTrigger>();
	        	if(comp != null) comp.enabled=false;
	        }
	    }
            
        if(Input.GetKey("right"))
        {
			female.transform.Rotate(new Vector3(0, 1, 0), -5);
        }
        else if(Input.GetKey("left"))
        {
        	female.transform.Rotate(new Vector3(0, 1, 0), 5);
        }
        else if(Input.GetKey("down"))
        {
        	//female.transform.Rotate(new Vector3(0, 0, 1), 5);
        }
        else if(Input.GetKey("up"))
        {
        	//female.transform.Rotate(new Vector3(0, 0, 1), -5);
        }
    }	
}

