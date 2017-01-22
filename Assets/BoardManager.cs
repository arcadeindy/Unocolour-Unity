﻿using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;

public class BoardManager : MonoBehaviour {

	public GameObject cardPrefab;
	public GameObject stackPrefab;
	public List<CardColor> deckColors;
	public List<int> deckColorNumbers;
	public GameObject cameraObject;
	public Text scoreText;
	new Camera camera;

	readonly static float DISTRIBUTETIME = 0.05f;
	readonly static int columns = 10;
	readonly static int rows = 5;
	readonly static CellPos lastCell = new CellPos (columns, rows);
	readonly static float halfcardwidth = 0.3f;
	readonly static float halfcardheight = 0.4f;
	readonly static Vector3 cellwidth = new Vector3 (0.83f, 0f, 0f);
	readonly static Vector3 cellheight = new Vector3 (0f, 1f, 0f);
	readonly static Vector3 cellsize = cellheight + cellwidth;
	readonly static Vector3 firstcellpos = new Vector3 (0f, 1f, 0f) - (columns - 1)/2f * cellwidth - (rows - 1)/2f * cellheight;

	//CardStack[,] cells = new CardStack[columns, rows];
	Dictionary<CellPos,CardStack> cells = new Dictionary<CellPos, CardStack> (lastCell.area);
	CardStack deck;
	int score;
	bool animating;
	bool stateTransition;
	int round;
	int cardsplayed;
	enum State {roundstart,shuffle,deal,play,collect,scoreboard,finalscoreboard};
	State state;

	BoardEngine board;


	void InstantiateBoard()
	{
		// Making the deck
		GameObject deckStack = Instantiate(stackPrefab,new Vector3(0f,-3f,0f),Quaternion.identity, transform) as GameObject;
		deck = deckStack.GetComponent<CardStack> ();
		deck.SetProperties(true,false,false);

		// Making the cards
		for (int i = 0; i < deckColors.Count; i++) {
			for (int j = 0; j < deckColorNumbers [i]; j++) {
				MakeCard (deckColors [i], deck);
			}
		}

		// Making the individual stacks.
		foreach (CellPos i in lastCell.Range()) {
			GameObject cellStack = Instantiate (stackPrefab, firstcellpos + i*cellsize, Quaternion.identity, transform) as GameObject;
			cells [i] = cellStack.GetComponent<CardStack> ();
			cells [i].SetProperties (true, true, true);
		}

		board = new BoardEngine (new CellPos (columns, rows));

	}

	void MakeCard (CardColor color, CardStack stack)
	{
		GameObject cardObject = Instantiate (cardPrefab, stack.transform.position, Quaternion.identity, transform) as GameObject;
		cardObject.GetComponent<Card> ().color = color;
		stack.ReceiveCard (cardObject);
	}

	IEnumerator DistributeToActiveCells(bool force=false)
	{
		foreach (CellPos i in lastCell.Range()) {
			if (cells [i].number > 0 || force) {
				cells [i].ReceiveCard (deck.SendCard ());
				yield return new WaitForSeconds (DISTRIBUTETIME);
			}
		}
		animating = false;
	}

	void SyncCardActives()
	{
		foreach (CellPos i in lastCell.Range()) {
			cells [i].active = board.CellActive (i);
		}
	}
	Dictionary<CellPos,CardColor> cellsAsCardColor {
		get {
			Dictionary<CellPos,CardColor> result = new Dictionary<CellPos, CardColor> (lastCell.area);
			foreach (CellPos i in  lastCell.Range()) {
				result [i] = cells [i].topCardColor;
			}
			return result;
		}
	}

	int Triangle(int n)
	{
		return n * (n + 1) / 2;
	}

	void RecalculateMoves()
	{
		board.RecalculateActiveShapes (cellsAsCardColor);
		SyncCardActives ();
	}
	void AddCardtoMove(CellPos pos)
	{
		if (board.CellActive (pos)) {
			cardsplayed += 1;
			cells [pos].ReceiveCard (deck.SendCard ());
			board.AddCellToMove (pos);
			SyncCardActives ();
		}
	}
	void Shuffle()
	{
		deck.Shuffle ();
	}
	void Deal()
	{
		animating = true;
		StartCoroutine(DistributeToActiveCells (round == 1));
	}
	void Collect()
	{
		for (int i = 0; i < round; i++) {
			foreach (CellPos pos in lastCell.Range()){
				if (cells [pos].number > 0) {
					deck.ReceiveCard (cells [pos].SendCard ());
				}
			}
		}
	}
	void Score()
	{
		foreach (CellPos pos in lastCell.Range()) {
			score += round * Triangle (cells [pos].number);
		}
		scoreText.text = score.ToString ();
	}

	CellPos GetMouseCell()
	{
		Vector3 currentPos = camera.ScreenToWorldPoint (Input.mousePosition);
		//Debug.Log (currentPos);
		int i, j;
		for (i = 0; i < columns; i++) {
			if (Mathf.Abs ((currentPos - firstcellpos - i * cellwidth).x) < halfcardwidth)
				break;
		}
		for (j = 0; j < rows; j++) {
			//Debug.Log ((currentPos - firstcellpos - j * cellheight).y.ToString ());
			if (Mathf.Abs ((currentPos - firstcellpos - j * cellheight).y) < halfcardheight)
				break;
		}

		if (i < columns && j < rows) {
			return new CellPos(i, j);
		}
		return null;
	}

	void Start()
	{
		camera = cameraObject.GetComponent<Camera> ();
		InstantiateBoard ();
		state = State.roundstart;
		round = 0;
		score = 0;
		scoreText.text = score.ToString ();
		animating = false;
		stateTransition = false;

	}
		
	void Update(){

		CellPos mousePos = GetMouseCell ();

		if (mousePos != null && !animating) {
			cells [mousePos].Hover ();
		}

		switch (state) {
		case State.roundstart:
			round += 1;
			state = State.shuffle;
			stateTransition = true;
			break;
		case State.shuffle:
			if (stateTransition) {
				stateTransition = false;
				Shuffle ();
			} 
			if (!animating) {
				if (round == 1) {
					state = State.deal;
				} else {
					state = State.play;
				}
				stateTransition = true;
			}
			break;
		case State.deal:
			if (stateTransition) {
				stateTransition = false;
				Deal ();
			} 
			if (!animating) {
				state = State.play;
				stateTransition = true;
			}
			break;
		case State.play:
			if (stateTransition) {
				stateTransition = false;
				cardsplayed = 0;
				RecalculateMoves ();
				if (deck.number < 4 || !board.IsAnyMovePossible ()) {
					state = State.collect;
					stateTransition = true;
				}
			} 
			if (!animating) {
				if (Input.GetMouseButtonDown (0) && mousePos != null) {
					AddCardtoMove (mousePos);
					if (cardsplayed == 4) {
						stateTransition = true;
					}
				}
			}
			break;
		case State.collect:
			if (stateTransition) {
				stateTransition = false;
				Collect ();
			}
			if (!animating) {
				state = State.scoreboard;
				stateTransition = true;
			}
			break;
		case State.scoreboard:
			if (stateTransition) {
				stateTransition = false;
				Score ();
			}
			if (!animating) {
				if (deck.number == 108) {
					state = State.finalscoreboard;
				} else {
					state = State.roundstart;
				}
				stateTransition = true;
			}
			break;
		}
			

			
	}

//		if (Time.realtimeSinceStartup > 2 && foo) {
//			GetComponentInChildren<CardStack> ().shuffle ();
//			StartCoroutine(DistributeToActiveCells (true));
//			foo = false;
//			Debug.Log ("shuffle");
//		}
//
//		CellPos NewHoverCell = GetMouseCell ();
//		if (NewHoverCell != null) {
//			cells[NewHoverCell].Hover();
//		}
//
//		if (Input.GetMouseButtonDown (0)) {
//			if (NewHoverCell != null) {
//				cells[NewHoverCell].ReceiveCard (deck.SendCard ());
//				AddCardtoMove (NewHoverCell);
//			}
//		}
//
}


