// upillu3352.zip[KK] GOLマクロ集(全員脱衣、衆人環視H、女の子回転、スタジオキャラ一括変更)v.1.0
using UnityEngine;

public class NudeMgr : MonoBehaviour
{
	public static bool nude = false;
	public static int mode = 0;

	public void Update()
	{
		var clothing = false;
		if (Input.GetKeyDown("[0]")) //テンキー0
		{
			if (nude)
			{
				mode++;
				if (mode > 3) mode = 0;
			}
			else
			{
				nude = true;
			}
		}
		else if (Input.GetKeyDown("[1]")) //テンキー1
		{
			nude = false;
			clothing = true;
		}

		if (nude || clothing)
		{
			var charaDict = Manager.Character.Instance.dictEntryChara;
			if (charaDict == null || charaDict.Count == 0) return;

			for (int i = charaDict.Count - 1; i >= 0; i--)
			{
				if (charaDict[i].sex != 0)
				{
					var female = charaDict[i];

					if (nude)
					{
						if (mode == 0) //上半身半脱、下半身半脱
						{
							female.SetClothesStateAll(0);
							if (female.hiPoly) //会話、H時
							{
								female.fileStatus.clothesState[0] = 1; //トップス、半脱
								female.fileStatus.clothesState[2] = 1; //ブラ、半脱
								female.fileStatus.clothesState[3] = 1; //ショーツ、半脱（めくれ）
								female.fileStatus.clothesState[5] = 3; //パンスト、全脱
								female.fileStatus.clothesState[1] = 1; //ボトムス、半脱
							}
							else //移動時
							{
								female.fileStatus.clothesState[0] = 0; //トップス、着
								female.fileStatus.clothesState[2] = 0; //ブラ、着
								female.fileStatus.clothesState[3] = 3; //ショーツ、全脱
								female.fileStatus.clothesState[5] = 0; //パンスト、着
								female.fileStatus.clothesState[1] = 0; //ボトムス、着
							}
						}
						else if (mode == 1) //上半身裸、下半身半脱
						{
							female.SetClothesStateAll(0);
							if (female.hiPoly) //会話、H時
							{
								female.fileStatus.clothesState[1] = 1; //ボトムス、半脱
								female.fileStatus.clothesState[3] = 1; //ショーツ、半脱（めくれ）
							}
							else //移動時
							{
								female.fileStatus.clothesState[1] = 0; //ボトムス、着
								female.fileStatus.clothesState[3] = 3; //ショーツ、全脱
							}
							female.fileStatus.clothesState[0] = 3; //トップス、全脱
							female.fileStatus.clothesState[2] = 3; //ブラ、全脱
						}
						else if (mode == 2) //上半身半脱、下半身裸
						{
							female.SetClothesStateAll(0);
							if (female.hiPoly) //会話、H時
							{
								female.fileStatus.clothesState[0] = 1; //トップス、半脱
								female.fileStatus.clothesState[2] = 1; //ブラ、半脱
								female.fileStatus.clothesState[3] = 2; //ショーツ、半脱（ひっかけ）
							}
							else //移動時
							{
								female.fileStatus.clothesState[0] = 0; //トップス、着
								female.fileStatus.clothesState[2] = 0; //ブラ、着
								female.fileStatus.clothesState[3] = 3; //ショーツ、全脱
							}
							female.fileStatus.clothesState[5] = 3; //パンスト、全脱
							female.fileStatus.clothesState[1] = 3; //ボトムス、全脱
						}
						else if (mode == 3) //全裸
						{
							female.SetClothesStateAll(3);
						}
					}
					else if (clothing) //着
					{
						female.SetClothesStateAll(0);
					}
				}
			}
			if (clothing) clothing = false;
		}
	}
}
