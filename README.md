# Tiny Chess Engine
This is the codebase for my submission to Sebastian Lague's Tiny Chess Challenge. Credit to Allyn Loyd for working on this with me.

# Note:
The entire chess framework is Sebastian Lague's. We did not contribute to it, we simply wrote our engine in the MyBot.cs file. All credit goes to Sebastian for the framework.

# Important Links
- [Sebastian's Introductory YouTube video](https://youtu.be/iScy18pVR58)
- [GitHub for the challenge](https://github.com/SebLague/Chess-Challenge)
- [Unofficial live leaderboard](https://chess.stjo.dev/) (We built MARCH and PrincessAtta - PrincessAtta v4)

# Our Approach
In my Swarm Intelligence class, we read a paper called [When ants play chess (Or can strategies emerge from tactical behaviours?)](https://link.springer.com/chapter/10.1007/BFb0027053). This paper describes an agent-based approach to evaluating moves using the following algorithm:  

- +10 points for every one of your pieces that sees the square and -10 for every one of your opponents pieces that see it
- If an opponent's piece is on the square, add its material value * 2
- Add the material value of every new piece that would be threatened/defended
- Subtract 10 and the material value of the moving piece (since making the move would relinquish its control of the square and risk its own material)
- Pawns add points equal to the rank of the square to encourage promotion

## First iteration
For our first bot (named MARCH after the paper), we faithfully implemented this algorithm since we wanted to see how it performed and maybe get an ELO estimate for it.  

While this algorithm works, we identified several problems with it:  
- It has no concept of space control
- It doesn't account for the pieces it stops defending
- It doesn't factor in cases where a move 'defended' a piece that was already defended by the moving piece
- It doesn't consider how many pieces are already attacking the piece
- It doesn't consider the pieces that will be threatened if the move is made
- There is not much of an incentive to castle
- For promotions, all pieces are sometimes favored equally
- It can't see through pieces (i.e. batteries)

## Second iteration
For our second and third iterations (PrincessAtta and PrincessAtta v2), we added calculations for most of these, weighted by arbitrary constants. We could not figure out how to identify batteries or re-defending pieces, but the other factors were enough to improve its performance.

## Third iteration
For our third iteration (PrincessAtta v3), we attempted to use a genetic algorithm to optimize the weights for each of the factors in evaluating a move. We would make each genome in the population play 10 games against PrincessAtta v2 and fitness would be determined by the win/loss rate. We were able to achieve weights that achieved a score of 6.5/10, though it was not clear if these weights actually performed better on the leaderboard since we were only training against the same algorithm which was already very passive.

## Fourth iteration
For our final iteration (PrincessAtta v4), we looked for other approaches to agent-based chess engines, and found a paper called [Multi-Agent Based Chess Move Generator System: Taking into Account Local Environments](https://citeseerx.ist.psu.edu/document?repid=rep1&type=pdf&doi=d1cd619d61d111947a46bf0bc34d7ee4018b8447) that extended the algorithm from the first paper. The idea this paper presented was that instead of the agents deciding on moves based on their marks, they give all the information to the King who then makes the decision. In short, they used the evaluation function from the first paper to guide a minimax tree search with alpha/beta pruning.  

We implemented this algorithm using our improved evaluation function and a negamax tree search, which is just a simplified minimax tree search for zero-sum games like Chess. This algorithm was significantly stronger than our previous iterations at depths of around 8, however it was much more computationally expensive. Since the chess framework for this algorithm was not designed for our algorithm, many of our evaluation factors were very costly to compute, so the bot frequently reached the end of its time limit at depths of 5+. There might be some clever bitboard math that could speed it up but we couldn't wind anything.


## Takeaways
Overall, this was a fun challenge. We were able to learn from and improve upon two agent-based chess engines. Though these algorithms were not very effective in the grand scheme of things, it was certainly interesting to look at a very exotic approach to chess algorithms.
