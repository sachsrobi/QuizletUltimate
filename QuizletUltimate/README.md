# QuizletUltimate

**A desktop quiz and learning application that turns study material into interactive quizzes with the help of AI.**

QuizletUltimate is a C# WPF desktop application designed to make studying more interactive and efficient. Users can generate personalized quiz content with an AI chatbot, import it into the application, and practice using five different question types and dedicated learning modes.

## Features

- **AI-assisted quiz creation** — Generate quiz content from your own study materials using an AI chatbot such as ChatGPT or Claude.
- **JSON import** — Import generated quiz files into the application.
- **Five question types:**
  - Multiple Choice
  - True/False
  - Text Input
  - Matching
  - Ordering
- **Multiple learning modes:**
  - **Learning:** Practice questions in random order.
  - **Revision:** Revisit questions answered incorrectly.
  - **Review:** Review challenging questions and performance statistics.
- **Keyboard shortcuts** — Navigate quizzes and submit answers efficiently.
- **Interactive exercises** — Answer matching and ordering questions using drag-and-drop interactions.

## How It Works

1. Download the sample quiz format from the application.
2. Copy the provided prompt into an AI chatbot.
3. Add your own study material or notes.
4. Ask the AI to generate quiz questions in the expected JSON format.
5. Download the generated JSON file and import it into QuizletUltimate.
6. Choose your preferred question categories and start studying.

This workflow allows users to create personalized quizzes for different subjects without manually entering every question.

## Screenshots

Screenshots of the application are included in this fodler to demonstrate its interface and learning features.

## Built With

- C#
- .NET
- WPF (Windows Presentation Foundation)
- XAML
- JSON

## Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| Space / Enter | Submit an answer or continue |
| R | Restart the quiz |
| Escape | Quit the application |
| 1–9 | Select multiple-choice answers |
| T / F | Answer True/False questions |
| Enter | Submit a text answer |

## What I Learned

Developing QuizletUltimate gave me practical experience in building a desktop application with C# and WPF, creating interactive user interfaces with XAML, working with structured JSON data, and implementing different quiz and learning workflows.
The project also explores how AI-generated content can be incorporated into a practical application to help users create personalized learning materials.

## Getting Started

1. Clone or download this repository.
2. Open the solution in Visual Studio.
3. Restore the required NuGet packages, if necessary.
4. Build and run the application.

Use the sample quiz format provided by the application when generating your own quiz files.

## Project Status

QuizletUltimate is a personal project developed to explore desktop application development and AI-assisted learning workflows.