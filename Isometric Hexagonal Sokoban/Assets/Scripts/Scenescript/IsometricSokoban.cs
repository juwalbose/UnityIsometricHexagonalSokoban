using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class IsometricSokoban : MonoBehaviour {
	public string levelName;//name of the text file in resources folder
	public float tileSize;//we will use this as tile width & half of it as tileheight/size of other elements
	
	//tile values for different tile types
	public int invalidTile;
	public int groundTile;
	public int destinationTile;
	public int heroTile;
	public int ballTile;
	public int heroOnDestinationTile;
	public int ballOnDestinationTile;

	public Color destinationColor;//destination tile has a different color

	//sprites for different tiles
	public Sprite tileSprite;
	public Sprite heroSprite;
	public Sprite ballSprite;
	public Sprite blockSprite;//new isometric block, just a hexagon really

	//the user input keys
	public KeyCode[] userInputKeys;//up, right, down, left
	int[,] levelData;//level array
	int rows;
	int cols;
	Vector2 middleOffset=new Vector2();//offset for aligning the level to middle of the screen
	int ballCount;//number of balls in level
	GameObject hero;//out triangular hero
	Dictionary<GameObject,Vector2> occupants;//reference to balls & hero
	bool gameOver;

	void Start () {
		gameOver=false;
		ballCount=0;
		occupants=new Dictionary<GameObject, Vector2>();
		ParseLevel();//load text file & parse our level 2d array
		CreateLevel();//create the level based on the array
	}
	void ParseLevel(){
		TextAsset textFile = Resources.Load (levelName) as TextAsset;
		string[] lines = textFile.text.Split (new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);//split by new line, return
		string[] nums = lines[0].Split(new[] { ',' });//split by ,
		rows=lines.Length;//number of rows
		cols=nums.Length;//number of columns
		levelData = new int[rows, cols];
        for (int i = 0; i < rows; i++) {
			string st = lines[i];
            nums = st.Split(new[] { ',' });
			for (int j = 0; j < cols; j++) {
                int val;
                if (int.TryParse (nums[j], out val)){
                	levelData[i,j] = val;
				}
                else{
                    levelData[i,j] = invalidTile;
				}
            }
        }
	}
	void CreateLevel(){
		//calculate the offset to align whole level to scene middle
		middleOffset.x=cols*tileSize*0.0625f+tileSize*0.125f;//this is changed for isometric
		middleOffset.y=rows*tileSize*0.25f+tileSize*0.25f;//this is changed for isometric
		GameObject tile;
		SpriteRenderer sr;
		GameObject ball;
		int destinationCount=0;
		for (int i = 0; i < rows; i++) {
			for (int j = 0; j < cols; j++) {
                int val=levelData[i,j];
				if(val!=invalidTile){//a valid tile
					tile = new GameObject("tile"+i.ToString()+"_"+j.ToString());//create new tile
					tile.transform.localScale=new Vector2(tileSize-1,(tileSize-1)/2);//size is critical for isometric shape
					sr = tile.AddComponent<SpriteRenderer>();//add a sprite renderer
					sr.sprite=tileSprite;//assign tile sprite
					tile.transform.position=GetScreenPointFromLevelIndices(i,j);//place in scene based on level indices
					if(val==destinationTile){//if it is a destination tile, give different color
						sr.color=destinationColor;
						destinationCount++;//count destinations
					}else{
						if(val==heroTile){//the hero tile
							hero = new GameObject("hero");
							hero.transform.localScale=Vector2.one*(tileSize/2);//we use half the tilesize for occupants
							sr = hero.AddComponent<SpriteRenderer>();
							sr.sprite=heroSprite;
							sr.sortingOrder=1;//hero needs to be over the ground tile
							sr.color=Color.red;
							hero.transform.position=GetScreenPointFromLevelIndices(i,j);
							occupants.Add(hero, new Vector2(i,j));//store the level indices of hero in dict
						}else if(val==ballTile){//ball tile
							ballCount++;//increment number of balls in level
							ball = new GameObject("ball"+ballCount.ToString());
							ball.transform.localScale=Vector2.one*(tileSize/2);//we use half the tilesize for occupants
							sr = ball.AddComponent<SpriteRenderer>();
							sr.sprite=ballSprite;
							sr.sortingOrder=1;//ball needs to be over the ground tile
							sr.color=Color.black;
							ball.transform.position=GetScreenPointFromLevelIndices(i,j);
							occupants.Add(ball, new Vector2(i,j));//store the level indices of ball in dict
						}
					}
				}else{//additional block tile for isometric for all invalid tiles (-1)
					tile = new GameObject("block"+i.ToString()+"_"+j.ToString());//create new tile
					float rootThree=Mathf.Sqrt(3);
					float newDimension= 2*tileSize/rootThree;
					tile.transform.localScale=new Vector2(newDimension,tileSize);//we need to set some height
					sr = tile.AddComponent<SpriteRenderer>();//add a sprite renderer
					sr.sprite=blockSprite;//assign block sprite
					sr.sortingOrder=1;//this also need to have higher sorting order
					Color c= Color.gray;
					c.a=0.9f;
					sr.color=c;
					tile.transform.position=GetScreenPointFromLevelIndices(i,j);//place in scene based on level indices
					occupants.Add(tile, new Vector2(i,j));//store the level indices of block in dict
				} 
            }
        }
		if(ballCount>destinationCount)Debug.LogError("there are more balls than destinations");
		DepthSort();//sort depth after placing tiles, assign proper sorting order
	}
    void Update(){
		if(gameOver)return;
		ApplyUserInput();//check & use user input to move hero and balls
	}

    private void ApplyUserInput()
    {
        if(Input.GetKeyUp(userInputKeys[0])){
			TryMoveHero(0);//up
		}else if(Input.GetKeyUp(userInputKeys[1])){
			TryMoveHero(1);//right
		}else if(Input.GetKeyUp(userInputKeys[2])){
			TryMoveHero(2);//down
		}else if(Input.GetKeyUp(userInputKeys[3])){
			TryMoveHero(3);//left
		}
    }
    private void TryMoveHero(int direction)
    {
        Vector2 heroPos;
		Vector2 oldHeroPos;
		Vector2 nextPos;
		occupants.TryGetValue(hero,out oldHeroPos);
		heroPos=GetNextPositionAlong(oldHeroPos,direction);//find the next array position in given direction
		
		if(IsValidPosition(heroPos)){//check if it is a valid position & falls inside the level array
			if(!IsOccuppied(heroPos)){//check if it is occuppied by a ball
				//move hero
				RemoveOccuppant(oldHeroPos);//reset old level data at old position
				hero.transform.position=GetScreenPointFromLevelIndices((int)heroPos.x,(int)heroPos.y);
				occupants[hero]=heroPos;
				if(levelData[(int)heroPos.x,(int)heroPos.y]==groundTile){//moving onto a ground tile
					levelData[(int)heroPos.x,(int)heroPos.y]=heroTile;
				}else if(levelData[(int)heroPos.x,(int)heroPos.y]==destinationTile){//moving onto a destination tile
					levelData[(int)heroPos.x,(int)heroPos.y]=heroOnDestinationTile;
				}
			}else{
				//we have a ball next to hero, check if it is empty on the other side of the ball
				nextPos=GetNextPositionAlong(heroPos,direction);
				if(IsValidPosition(nextPos)){
					if(!IsOccuppied(nextPos)){//we found empty neighbor, so we need to move both ball & hero
						GameObject ball=GetOccupantAtPosition(heroPos);//find the ball at this position
						if(ball==null)Debug.Log("no ball");
						RemoveOccuppant(heroPos);//ball should be moved first before moving the hero
						ball.transform.position=GetScreenPointFromLevelIndices((int)nextPos.x,(int)nextPos.y);
						occupants[ball]=nextPos;
						if(levelData[(int)nextPos.x,(int)nextPos.y]==groundTile){
							levelData[(int)nextPos.x,(int)nextPos.y]=ballTile;
						}else if(levelData[(int)nextPos.x,(int)nextPos.y]==destinationTile){
							levelData[(int)nextPos.x,(int)nextPos.y]=ballOnDestinationTile;
						}
						RemoveOccuppant(oldHeroPos);//now move hero
						hero.transform.position=GetScreenPointFromLevelIndices((int)heroPos.x,(int)heroPos.y);
						occupants[hero]=heroPos;
						if(levelData[(int)heroPos.x,(int)heroPos.y]==groundTile){
							levelData[(int)heroPos.x,(int)heroPos.y]=heroTile;
						}else if(levelData[(int)heroPos.x,(int)heroPos.y]==destinationTile){
							levelData[(int)heroPos.x,(int)heroPos.y]=heroOnDestinationTile;
						}
					}
				}
			}
			DepthSort();//sort depth after movement
			CheckCompletion();//check if all balls have reached destinations
		}
    }
	private void DepthSort()
    {
        int depth=1;
		SpriteRenderer sr;
		Vector2 pos=new Vector2();
		for (int i = 0; i < rows; i++) {
			for (int j = 0; j < cols; j++) {
                int val=levelData[i,j];
				if(val!=groundTile && val!=destinationTile){//a tile which needs depth sorting
					pos.x=i;
					pos.y=j;
					GameObject occuppant=GetOccupantAtPosition(pos);//find the occuppant at this position
					if(occuppant==null)Debug.Log("no occuppant");
					sr=occuppant.GetComponent<SpriteRenderer>();
					sr.sortingOrder=depth;//assign new depth
					depth++;//increment depth
				}
			}
		}
    }
    private void CheckCompletion()
    {
        int ballsOnDestination=0;
		for (int i = 0; i < rows; i++) {
			for (int j = 0; j < cols; j++) {
                if(levelData[i,j]==ballOnDestinationTile){
					ballsOnDestination++;
				}
			}
		}
		if(ballsOnDestination==ballCount){
			Debug.Log("level complete");
			gameOver=true;
		}
    }
    private GameObject GetOccupantAtPosition(Vector2 objPos)
    {//loop through the occupants to find the ball at given position
        GameObject ball;
		foreach (KeyValuePair<GameObject, Vector2> pair in occupants)
		{
			if (pair.Value == objPos)
			{
				ball = pair.Key;
				return ball;
			}
		}
		return null;
    }

    private void RemoveOccuppant(Vector2 objPos)
    {
        if(levelData[(int)objPos.x,(int)objPos.y]==heroTile||levelData[(int)objPos.x,(int)objPos.y]==ballTile){
			levelData[(int)objPos.x,(int)objPos.y]=groundTile;//ball moving from ground tile
		}else if(levelData[(int)objPos.x,(int)objPos.y]==heroOnDestinationTile){
			levelData[(int)objPos.x,(int)objPos.y]=destinationTile;//hero moving from destination tile
		}else if(levelData[(int)objPos.x,(int)objPos.y]==ballOnDestinationTile){
			levelData[(int)objPos.x,(int)objPos.y]=destinationTile;//ball moving from destination tile
		}
    }

    private bool IsOccuppied(Vector2 objPos)
    {//check if there is a ball at given array position
        return (levelData[(int)objPos.x,(int)objPos.y]==ballTile || levelData[(int)objPos.x,(int)objPos.y]==ballOnDestinationTile);
    }

    private bool IsValidPosition(Vector2 objPos)
    {//check if the given indices fall within the array dimensions
        if(objPos.x>-1&&objPos.x<rows&&objPos.y>-1&&objPos.y<cols){
			return levelData[(int)objPos.x,(int)objPos.y]!=invalidTile;
		}else return false;
    }

    private Vector2 GetNextPositionAlong(Vector2 objPos, int direction)
    {
        switch(direction){
			case 0:
			objPos.x-=1;//up
			break;
			case 1:
			objPos.y+=1;//right
			break;
			case 2:
			objPos.x+=1;//down
			break;
			case 3:
			objPos.y-=1;//left
			break;
		}
		return objPos;
    }
	Vector2 GetScreenPointFromLevelIndices(int row,int col){
		//converting indices to position values, col determines x & row determine y
		Vector2 tempPt=CartesianToIsometric(new Vector2(col*tileSize/2,row*tileSize/2));//removed the '-' inthe y part as axis correction can happen after coversion
		tempPt.x-=middleOffset.x;//we apply the offset outside the coordinate conversion to align the level in screen middle
		tempPt.y*=-1;//unity y axis correction
		tempPt.y+=middleOffset.y;//we apply the offset outside the coordinate conversion to align the level in screen middle
		return tempPt;
	}
	/*//the reverse methods to find indices from a screen point
	Vector2 GetLevelIndicesFromScreenPoint(float xVal,float yVal){
		return new Vector2((int)(yVal-middleOffset.y)/-tileSize,(int)(xVal+middleOffset.x)/tileSize);
	}
	Vector2 GetLevelIndicesFromScreenPoint(Vector2 pos){
		return GetLevelIndicesFromScreenPoint(pos.x,pos.y);
	}*/
	Vector2 CartesianToIsometric(Vector2 cartPt){
		Vector2 tempPt=new Vector2();
		tempPt.x=cartPt.x-cartPt.y;
		tempPt.y=(cartPt.x+cartPt.y)/2;
		return (tempPt);
	}
	/*//the reverse conversion method for isometric to cartesian coordinate conversion
	Vector2 IsometricToCartesian(Vector2 isoPt){
		Vector2 tempPt=new Vector2();
		tempPt.x=(2*isoPt.y+isoPt.x)/2;
		tempPt.y=(2*isoPt.y-isoPt.x)/2;
		return (tempPt);
	}*/
	public void RestartLevel(){
		//Application.LoadLevel(0);
		SceneManager.LoadScene(0);
	}
}
