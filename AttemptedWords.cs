using System.Collections.Generic;

namespace WordleSharp
{
    /// <summary>
    /// Holds the words played in a single game
    /// </summary>
    public class AttemptedWords
    {
        private IEnumerable<string> Words { get; }

        public AttemptedWords(IEnumerable<string> words)
        {
            Words = words;
        }

        public override string ToString()
        {
            return string.Join(",", Words);
        }

    }
}