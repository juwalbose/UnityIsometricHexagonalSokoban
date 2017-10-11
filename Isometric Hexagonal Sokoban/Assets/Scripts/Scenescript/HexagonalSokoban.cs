using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class HexagonalSokoban : MonoBehaviour {
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
	float sideLength;//the length of each side of the heaxagon or half the distance between pointy ends

	void Start () {
		gameOver=false;
		ballCount=0;
		sideLength=tileSize*0.5f;
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
		float tileWidth=sideLength*Mathf.Sqrt(3);
		middleOffset.x=cols*tileWidth+tileWidth*0.5f;//this is changed for hexagonal
		middleOffset.y=rows*tileSize*3/4+tileSize*0.75f;//this is changed for isometric
		GameObject tile;
		SpriteRenderer sr;
		GameObject ball;
		int destinationCount=0;
		for (int i = 0; i < rows; i++) {
			for (int j = 0; j < cols; j++) {
                int val=levelData[i,j];
				if(val!=invalidTile){//a valid tile
					tile = new GameObject("tile"+i.ToString()+"_"+j.ToString());//create new tile
					tile.transform.localScale=new Vector2(tileSize-1,(tileSize-1));//size is critical for isometric shape
					sr = tile.AddComponent<SpriteRenderer>();//add a sprite renderer
					sr.sprite=tileSprite;//assign tile sprite
					tile.transform.position=GetScreenPointFromLevelIndices(i,j);//place in scene based on level indices
					if(val==destinationTile){//if it is a destination tile, give different color
						sr.color=destinationColor;
						destinationCount++;//count destinations
					}else{
						if(val==heroTile){//the hero tile
							hero = new GameObject("hero");
							hero.transform.localScale=Vector2.one*(tileSize/1.5f);//we use half the tilesize for occupants
							sr = hero.AddComponent<SpriteRenderer>();
							sr.sprite=heroSprite;
							sr.sortingOrder=1;//hero needs to be over the ground tile
							sr.color=Color.red;
							hero.transform.position=GetScreenPointFromLevelIndices(i,j);
							occupants.Add(hero, new Vector2(i,j));//store the level indices of hero in dict
						}else if(val==ballTile){//ball tile
							ballCount++;//increment number of balls in level
							ball = new GameObject("ball"+ballCount.ToString());
							ball.transform.localScale=Vector2.one*(tileSize/1.5f);//we use half the tilesize for occupants
							sr = ball.AddComponent<SpriteRenderer>();
							sr.sprite=ballSprite;
							sr.sortingOrder=1;//ball needs to be over the ground tile
							sr.color=Color.black;
							ball.transform.position=GetScreenPointFromLevelIndices(i,j);
							occupants.Add(ball, new Vector2(i,j));//store the level indices of ball in dict
						}
					}
				}
            }
        }
		if(ballCount>destinationCount)Debug.LogError("there are more balls than destinations");
	}
    void Update(){
		if(gameOver)return;
		ApplyUserInput();//check & use user input to move hero and balls
	}

    private void ApplyUserInput()
    {//we have 6 directions of motion controlled by e,d,x,z,a,w in a cyclic sequence starting with NE to NW
        if(Input.GetKeyUp(userInputKeys[0])){
			TryMoveHero(0);//north east
		}else if(Input.GetKeyUp(userInputKeys[1])){
			TryMoveHero(1);//east
		}else if(Input.GetKeyUp(userInputKeys[2])){
			TryMoveHero(2);//south east
		}else if(Input.GetKeyUp(userInputKeys[3])){
			TryMoveHero(3);//south west
		}else if(Input.GetKeyUp(userInputKeys[4])){
			TryMoveHero(4);//west
		}else if(Input.GetKeyUp(userInputKeys[5])){
			TryMoveHero(5);//north west
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
			CheckCompletion();//check if all balls have reached destinations
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
    {//this method is completely changed to accomodate thedifferent way neighbours are found in hexagonal logic
        objPos=HexHelperHorizontal.offsetToAxial(objPos);//convert from offset to axial
		List<Vector2> neighbours= HexHelperHorizontal.getNeighbors(objPos);
		objPos=neighbours[direction];//the neighbour list follows the same order sequence
		objPos=HexHelperHorizontal.axialToOffset(objPos);//convert back from axial to offset
		return objPos;
    }
	Vector2 GetScreenPointFromLevelIndices(int row,int col){
		//converting indices to position values, col determines x & row determine y
		Vector2 tempPt=new Vector2(row,col);
		tempPt=HexHelperHorizontal.offsetToAxial(tempPt);//convert from offset to axial
		//convert axial point to screen point
		tempPt=HexHelperHorizontal.axialToScreen(tempPt,sideLength);
		tempPt.x-=middleOffset.x-Screen.width/2;//add offsets for middle align
		tempPt.y*=-1;//unity y axis correction
		tempPt.y+=middleOffset.y-Screen.height/2;
		return tempPt;
	}
	public void RestartLevel(){
		//Application.LoadLevel(0);
		SceneManager.LoadScene(0);
	}
}
