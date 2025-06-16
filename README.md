# WordleSharp
## Overview
WordleSharp provides advanced Wordle solving capabilities through PowerShell cmdlets, featuring multiple calculation algorithms and comprehensive analysis tools.

## Installation
Import the module in PowerShell:
```powershell
Import-Module .\WordleSharp.psd1
```

## Quick Start
Get help for any cmdlet:
```powershell
Get-Help Start-WordleAnalysis -Full
Get-Help Start-Autoplay -Examples
```

## Input Format Guide
WordleSharp uses a specific format to represent Wordle feedback:

### Basic Input Format
- Enter words followed by numbers to indicate letter status:
  - `0` = Gray (letter not in word - but you can omit this, see below)
  - `1` = Yellow (correct letter, wrong position)  
  - `2` = Green (correct letter, correct position)

### Examples
```
# If you guessed "arose" and got: A(gray), R(yellow), O(gray), S(green), E(gray)
arose
a0r1o0s2e0

# The system automatically adds '0' to letters without specifiers, so this is equivalent:
ar1os2e
```

### Special Input Patterns

#### Answer Verification
```powershell
# Check if a word is in the remaining possible solutions
word?
# Returns: True/False
```

#### Known Answer Mode
```powershell
# Score a guess against a known answer (for analysis)
guess,answer
# Example: arose,about
```

#### Correct Answer Declaration
```powershell
# Mark a word as the correct final answer
word!
# Example: about!
```

## Available Cmdlets

### Start-WordleAnalysis
Interactive analysis mode for solving Wordle puzzles step-by-step.
```powershell
Start-WordleAnalysis -CountOnly
# Starts the analysis engine with the default solving calculator, but doesn't tell you what word to play next, only how many words are left.
# This is the best mode for analysing your play as you play it.
```

### Start-Autoplay
Automated solving of Wordle puzzles.
```powershell
Start-Autoplay -StartWord "arose" -Answer "about" -Calculator CountReduction
```

### Get-BestStartWord
Find optimal starting words for given answers.
```powershell
Get-BestStartWord -Answer "about"
```

### Get-WordScore
"Scores" a given word given an answer using the x[0|1|2] format
```powershell
Get-WordScore -Word "arose" -Answer "about"
```

### Get-WordsContainingLetter
Find words containing specific letters.
```powershell
Get-WordsContainingLetter -Letter "aeiou"
```

## Calculator Types
Choose between different solving algorithms for calculating the next best word:

- **CountReduction**  : Minimizes remaining word count (default - slower)
- **LetterFrequency** : Prioritizes common letters (faster - but less optimal)

Specify with the `-Calculator` parameter:
```powershell
Start-WordleAnalysis -Calculator CountReduction
Start-Autoplay -Calculator LetterFrequency -StartWord "arose" -Answer "about"
```

## Performance Options
Control parallel processing for faster calculations:
```powershell
Start-WordleAnalysis -MaxDegreeOfParallelism 4
# This will default to the number of cores available on the running machine if not specified.
# Note that this parameter only affects the "CountReduction" calculator.
```

## Additional Resources
- [Calculator Usage Guide](CALCULATOR_USAGE.md)

For detailed help on any cmdlet, use `Get-Help <CmdletName> -Full`