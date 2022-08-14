using System.Collections.Generic;

namespace WordleSharp
{
    public class AttemptedWords
    {
        private IEnumerable<string> Words { get; }

        public AttemptedWords(IEnumerable<string> words)
        {
            Words = words;
        }

    }
}