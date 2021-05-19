using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace FourInARow
{
	struct Point {
		public int X,Y;
		public Point(int x, int y) {
			this.X = x;
			this.Y = y;
		}
	}

	internal class MCTS
	{
		public static readonly int FREE = 0;
		public static readonly int ME = 1;
		public static readonly int OPPONENT = 2;

		static Random random = new Random();
		public class Node
		{
			public int n;
			public double w;
			public int Action;
			public List<Node> Childs;

			static readonly int[] dx = { 0, 1, 1, 1 };
			static readonly int[] dy = { -1, -1, 0, 1 };
			public double score = -2;
			public double Score(int[,] board, int playX, int playY)
			{
				if (score != -2) return score;
				if (playY == -1) return -1; // continue playing
				for (int dir = 0; dir < 4; dir++)
				{
					int len = 1;
					for (int f = 1; ; f++)
					{
						int x_ = playX + f * dx[dir];
						int y_ = playY + f * dy[dir];
						if (x_ < 0 || y_ < 0 || x_ >= Connect4.WIDTH || y_ >= Connect4.HEIGHT || board[x_, y_] != board[playX, playY]) break;
						len++;
					}
					for (int f = -1; ; f--)
					{
						int x_ = playX + f * dx[dir];
						int y_ = playY + f * dy[dir];
						if (x_ < 0 || y_ < 0 || x_ >= Connect4.WIDTH || y_ >= Connect4.HEIGHT || board[x_, y_] != board[playX, playY]) break;
						len++;
					}
					if (len >= 4)
					{
						score = 1;
						return 1; // win for player
					}
				}

				for (int x = 0; x < Connect4.WIDTH; x++)
				{
					if (board[x, Connect4.HEIGHT - 1] == FREE)
					{
						score = -1;
						return -1; // not finished
					}
				}

				score = 0.5;
				return 0.5; // draw
			}

			public Node SelectChild(int[,] board, int N)
			{
				if (Childs == null)
				{
					Childs = new List<Node>(Connect4.WIDTH);
					for (int x = 0; x < Connect4.WIDTH; x++)
					{
						if (board[x, Connect4.HEIGHT - 1] == FREE) Childs.Add(new Node() { Action = x });
					}
				}

				if (n < Childs.Count)
				{
					int swap = random.Next(n, Childs.Count);
					Node tmp = Childs[swap];
					Childs[swap] = Childs[n];
					Childs[n] = tmp;
					return Childs[n];
				}

				double best = 0;
				Node result = null;
				foreach (Node c in Childs)
				{
					double utc = c.w / c.n + Math.Sqrt(2 * Math.Log(N) / c.n);
					if (utc > best)
					{
						best = utc;
						result = c;
					}
				}
				return result;
			}

			public override string ToString()
			{
				if (score == 1) return "I win!";
				if (score == 0) return "I lose!";
				if (score == 0.5) return "Draw";
				return $"{w} / {n}  ({(100 * w / n).ToString("0.00")}%)";
			}
		}

		private static double Rollout(Node node, int player, int N, int[,] board, int playX, int playY)
		{
			double score = node.Score(board, playX, playY); // check result of opponent turn first
			if (score != -1)
			{
				node.n++;
				node.w += score;
				return 1 - score;
			}

			Node child = node.SelectChild(board, N);
			int pY = 0;
			for (int y = 0; y < Connect4.HEIGHT; y++)
			{
				if (board[child.Action, y] == FREE)
				{
					board[child.Action, y] = player;
					pY = y;
					break;
				}
			}

			double result = Rollout(child, 3 - player, N, board, child.Action, pY);
			node.n++;
			node.w += result;

			// MCTS solver
			if (child.score == 1)
				node.score = 0;

			bool win = true;
			foreach (Node c in node.Childs)
			{
				win &= c.score == 0;
				if (!win) break;
			}
			if (win) node.score = 1;

			return 1 - result;
		}

		public static Node Run(Node root, int[,] board, int time)
		{
			Stopwatch sw = Stopwatch.StartNew();
			if (root == null) root = new Node();

			while (root.n % 128 != 0 || sw.ElapsedMilliseconds < time)
			{
				Rollout(root, ME, root.n, (int[,])board.Clone(), -1, -1);
				if (root.score >= 0) break;
			}

			List<Node> candidates = root.Childs.Where(c => c.score == 1).ToList();
			if (candidates.Count > 0) return candidates.OrderBy(c => c.n).Last(); // winning move
			candidates = root.Childs.ToList();
			if (!candidates.Any(c => c.score != 0)) return candidates.OrderBy(c => c.n).Last(); // losing anyway
			List<Node> draw = candidates.Where(c => c.score == 0.5).ToList();
			List<Node> stillPlaying = candidates.Where(c => c.score == -1).ToList();
			if (draw.Count == 0) return stillPlaying.OrderBy(c => c.n).Last(); // classic MCTS choice
			if (stillPlaying.Count == 0) return draw.OrderBy(c => c.n).Last(); // draw with the longest path, hope for opponent mistakes

			Node playCandidate = stillPlaying.OrderBy(c => c.n).Last();
			if (playCandidate.w / playCandidate.n < 0.5) return draw.OrderBy(c => c.n).Last(); // take draw, don't risk anything
			return playCandidate; // try to enforce win
		}
	}

	internal class Connect4
	{
		public static readonly int WIDTH = 7;
		public static readonly int HEIGHT = 6;
		private int[,] board = new int[WIDTH, HEIGHT];
		private MCTS.Node root;

		public string PlayAI(int time)
		{
			root = MCTS.Run(root, board, time);

			int x = root.Action, y = 0;
			while (board[x, y] != MCTS.FREE) y++;
			board[x, y] = MCTS.ME;

			return root.ToString();
		}

		public List<Point> WinningRow()
		{
			int[] dx = { 0, 1, 1, 1 };
			int[] dy = { -1, -1, 0, 1 };
			for (int x = 0; x < WIDTH; x++)
			{
				for (int y = 0; y < HEIGHT; y++)
				{
					if (board[x, y] == MCTS.FREE) continue;
					for (int dir = 0; dir < dx.Length; dir++)
					{
						List<Point> result = new List<Point> { new Point(x, y) };
						for (int len = 1; len < 4; len++)
						{
							int x_ = x + len * dx[dir];
							int y_ = y + len * dy[dir];
							if (x_ >= 0 && x_ < WIDTH && y_ >= 0 && y_ < HEIGHT && board[x, y] == board[x_, y_]) result.Add(new Point(x_, y_));
						}
						if (result.Count == 4) return result;
					}
				}
			}
			return null;
		}

		public bool CanPlay(int x)
		{
			if (WinningRow() != null) return false;
			return board[x, HEIGHT - 1] == MCTS.FREE;
		}

		public void Play(int x)
		{
			int y = 0;
			while (board[x, y] != MCTS.FREE) y++;
			board[x, y] = MCTS.OPPONENT;

			if (root != null)
				root = root.Childs.FirstOrDefault(c => c.Action == x);
		}

		public string PrintBoard() {
			StringBuilder result = new StringBuilder("0 1 2 3 4 5 6\n");
			for (int y = HEIGHT - 1; y >= 0; y--) {
				for (int x = 0; x < WIDTH; x++) {
					if (board[x,y] == MCTS.ME) result.Append("O ");
					else if (board[x,y] == MCTS.OPPONENT) result.Append("X ");
					else result.Append(". ");
				}
				result.Append("\n");
			}
			return result.ToString();
		}


		public static void Main (string[] args)
		{
			Connect4 c4 = new Connect4();

			while (true) {
				Console.WriteLine(c4.PrintBoard());
				if (c4.WinningRow() != null) {
					Console.WriteLine("you lost");
					return;
				}
				int column = -1;
				do {
					Console.Write("Choose your column: ");
					column = int.Parse(Console.ReadLine());
				} while (!c4.CanPlay(column));
				c4.Play(column);
				Console.WriteLine();
				Console.WriteLine(c4.PrintBoard());

				if (c4.WinningRow() != null) {
					Console.WriteLine("you won");
					return;
				}
				var node = c4.PlayAI(1000);
				Console.WriteLine("AI stats: " + node);
			}
		}
	}
}
