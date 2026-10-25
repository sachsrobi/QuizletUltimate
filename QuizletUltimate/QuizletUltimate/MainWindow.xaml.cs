using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Newtonsoft.Json;
using System.IO;
using Microsoft.Win32;

namespace QuizletUltimate
{
    public partial class MainWindow : Window
    {
        private List<Question> questions;
        private List<Question> filteredQuestions;
        private Question currentQuestion;
        private int correctCount = 0;
        private int incorrectCount = 0;
        private Random random = new Random();
        private List<Border> movableBoxes = new List<Border>();
        private Dictionary<Border, Point> dragStartPoints = new Dictionary<Border, Point>();
        private bool isAnswerSubmitted = false;
        private Dictionary<Button, bool> multipleChoiceButtonStates = new Dictionary<Button, bool>();
        private Dictionary<Button, int> multipleChoiceButtonNumbers = new Dictionary<Button, int>();
        private List<string> askedQuestionIds = new List<string>();
        private List<string> incorrectQuestionIds = new List<string>();
        private bool isRevisionMode = false;
        private TextBlock correctAnswerTextBlock;
        private List<string> questionOrder = new List<string>(); // Track order questions were asked
        private Dictionary<string, int> questionMistakeCount = new Dictionary<string, int>(); // Track how many times each question was answered incorrectly

        public MainWindow()
        {
            InitializeComponent();
            ModeText.Text = "Please import a JSON file";
            QuestionHeaderText.Text = "Waiting for Input";
            Dispatcher.Invoke(() => ModeText.InvalidateVisual());
            System.Diagnostics.Debug.WriteLine($"Initial ModeText: {ModeText?.Text}");
            SubmitButton.IsEnabled = false;
            NextQuestionButton.IsEnabled = false;
            RestartButton.IsEnabled = false;
        }

        private void ImportFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Select a Quiz JSON File"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string json = File.ReadAllText(openFileDialog.FileName);
                    questions = JsonConvert.DeserializeObject<List<Question>>(json);
                    if (questions == null || questions.Count == 0)
                    {
                        System.Diagnostics.Debug.WriteLine("No questions loaded from JSON.");
                        questions = new List<Question>();
                        ModeText.Text = "No Questions Loaded";
                        Dispatcher.Invoke(() => ModeText.InvalidateVisual());
                        System.Diagnostics.Debug.WriteLine("No questions loaded, setting ModeText to 'No Questions Loaded'");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Loaded {questions.Count} questions.");
                        ApplyFilters();
                        ModeText.Text = "Learning";
                        Dispatcher.Invoke(() => ModeText.InvalidateVisual());
                        LoadRandomQuestion();
                        SubmitButton.IsEnabled = true;
                        RestartButton.IsEnabled = true;
                        UpdateQuestionCount(0);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading questions: {ex.Message}");
                    questions = new List<Question>();
                    ModeText.Text = "Error Loading Questions";
                    Dispatcher.Invoke(() => ModeText.InvalidateVisual());
                    MessageBox.Show($"Error loading file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SampleButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string rootDirectory = @"C:\Users\Robi\source\repos\QuizletUltimate\QuizletUltimate";
                string sampleFilePath = Path.Combine(rootDirectory, "SampleQuestions.json");

                if (File.Exists(sampleFilePath))
                {
                    SaveFileDialog saveFileDialog = new SaveFileDialog
                    {
                        Filter = "JSON files (*.json)|*.json",
                        Title = "Save Sample Questions File",
                        FileName = "SampleQuestions.json"
                    };

                    if (saveFileDialog.ShowDialog() == true)
                    {
                        File.Copy(sampleFilePath, saveFileDialog.FileName, true);
                        MessageBox.Show("Sample file downloaded successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    MessageBox.Show("Sample file not found in the root directory.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error downloading sample file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CategoriesButton_Click(object sender, RoutedEventArgs e)
        {
            CategoriesPopup.IsOpen = !CategoriesPopup.IsOpen;
        }

        private void ApplyFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyFilters();
            CategoriesPopup.IsOpen = false;

            if (questions != null && questions.Count > 0)
            {
                askedQuestionIds.Clear();
                incorrectQuestionIds.Clear();
                isRevisionMode = false;
                LoadRandomQuestion();
            }
        }

        private void ApplyFilters()
        {
            if (questions == null) return;

            var enabledTypes = new List<string>();

            if (MultipleChoiceCheckBox.IsChecked == true) enabledTypes.Add("multiple_choice");
            if (TextInputCheckBox.IsChecked == true) enabledTypes.Add("text_input");
            if (TrueFalseCheckBox.IsChecked == true) enabledTypes.Add("true_false");
            if (MatchingCheckBox.IsChecked == true) enabledTypes.Add("matching");
            if (OrderingCheckBox.IsChecked == true) enabledTypes.Add("ordering");

            filteredQuestions = questions.Where(q => enabledTypes.Contains(q.Type)).ToList();
        }

        private void LoadRandomQuestion()
        {
            if (filteredQuestions == null || filteredQuestions.Count == 0)
            {
                QuestionText.Text = "No questions available. Select from Categories!";
                QuestionHeaderText.Text = "Waiting for Input";
                AnswerGrid.Children.Clear();
                SubmitButton.IsEnabled = false;
                NextQuestionButton.IsEnabled = false;
                AnswerIndicator.Visibility = Visibility.Hidden;
                IndicatorBorder.Visibility = Visibility.Hidden;
                return;
            }

            List<Question> availableQuestions;

            if (isRevisionMode)
            {
                // In revision mode, maintain the order from learning period
                var orderedIncorrectIds = questionOrder.Where(id => incorrectQuestionIds.Contains(id) && !askedQuestionIds.Contains(id)).ToList();
                availableQuestions = orderedIncorrectIds.Select(id => filteredQuestions.First(q => q.Id == id)).ToList();
            }
            else
            {
                availableQuestions = filteredQuestions.Where(q => !askedQuestionIds.Contains(q.Id)).ToList();
            }

            if (availableQuestions.Count == 0)
            {
                if (availableQuestions.Count == 0)
                {
                    if (!isRevisionMode && incorrectQuestionIds.Count > 0)
                    {
                        isRevisionMode = true;
                        ModeText.Text = "Revision";
                        Dispatcher.Invoke(() => ModeText.InvalidateVisual());
                        System.Diagnostics.Debug.WriteLine("Switched to Revision mode");
                        askedQuestionIds.Clear(); // Clear to start revision
                        var orderedIncorrectIds = questionOrder.Where(id => incorrectQuestionIds.Contains(id)).ToList();
                        availableQuestions = orderedIncorrectIds.Select(id => filteredQuestions.First(q => q.Id == id)).ToList();
                    }
                    else if (isRevisionMode && incorrectQuestionIds.Count > 0)
                    {
                        // Continue revision mode - reset asked questions for another round
                        askedQuestionIds.Clear();
                        var orderedIncorrectIds = questionOrder.Where(id => incorrectQuestionIds.Contains(id)).ToList();
                        availableQuestions = orderedIncorrectIds.Select(id => filteredQuestions.First(q => q.Id == id)).ToList();
                    }
                    else
                    {
                        // Show review for all cases when no questions remain
                        ShowCompletionReview();
                        return;
                    }
                }
            }

            isAnswerSubmitted = false;
            SubmitButton.IsEnabled = true;
            NextQuestionButton.IsEnabled = false;
            AnswerIndicator.Visibility = Visibility.Hidden;
            IndicatorBorder.Visibility = Visibility.Hidden;
            multipleChoiceButtonStates.Clear();
            multipleChoiceButtonNumbers.Clear();
            correctAnswerTextBlock = null;

            if (isRevisionMode)
            {
                // In revision mode, take the first question to maintain order
                currentQuestion = availableQuestions.First();
            }
            else
            {
                currentQuestion = availableQuestions[random.Next(availableQuestions.Count)];
            }
            askedQuestionIds.Add(currentQuestion.Id);

            // Track question order for revision
            if (!isRevisionMode && !questionOrder.Contains(currentQuestion.Id))
            {
                questionOrder.Add(currentQuestion.Id);
            }
            QuestionText.Text = currentQuestion.QuestionText ?? "Question not loaded.";

            // Update question header based on question type and content
            if (currentQuestion.Type == "true_false")
            {
                QuestionHeaderText.Text = "Statement";
            }
            else if (currentQuestion.QuestionText != null && currentQuestion.QuestionText.Contains("?"))
            {
                QuestionHeaderText.Text = "Question";
            }
            else
            {
                QuestionHeaderText.Text = "Task";
            }

            string questionType = GetQuestionTypeDisplayName(currentQuestion.Type);
            ModeText.Text = isRevisionMode ? $"Revision - {questionType}" : $"Learning - {questionType}";
            Dispatcher.Invoke(() => ModeText.InvalidateVisual());
            System.Diagnostics.Debug.WriteLine($"ModeText set to: {ModeText.Text}, isRevisionMode: {isRevisionMode}");

            AnswerGrid.Children.Clear();
            movableBoxes.Clear();
            dragStartPoints.Clear();

            switch (currentQuestion.Type)
            {
                case "multiple_choice":
                    LoadMultipleChoice();
                    break;
                case "text_input":
                    LoadTextInput();
                    break;
                case "true_false":
                    LoadTrueFalse();
                    break;
                case "matching":
                    LoadMatching();
                    break;
                case "ordering":
                    LoadOrdering();
                    break;
                default:
                    QuestionText.Text = "Invalid question type.";
                    SubmitButton.IsEnabled = false;
                    ModeText.Text = "Error";
                    Dispatcher.Invoke(() => ModeText.InvalidateVisual());
                    System.Diagnostics.Debug.WriteLine("Invalid question type, ModeText set to 'Error'");
                    break;
            }
        }

        private void ShowCompletionReview()
        {
            QuestionText.Text = "All questions completed!";
            SubmitButton.IsEnabled = false;
            NextQuestionButton.IsEnabled = false;
            AnswerIndicator.Visibility = Visibility.Hidden;
            IndicatorBorder.Visibility = Visibility.Hidden;
            ModeText.Text = "Completed";
            Dispatcher.Invoke(() => ModeText.InvalidateVisual());
            System.Diagnostics.Debug.WriteLine("All questions completed, ModeText set to 'Completed'");
            QuestionHeaderText.Text = "Review";

            // Show review of most struggled questions
            AnswerGrid.Children.Clear();

            var struggledQuestions = questionMistakeCount
                .OrderByDescending(kvp => kvp.Value)
                .Take(10) // Show top 10 most struggled questions (5 rows x 2 columns)
                .Select(kvp => questions.FirstOrDefault(q => q.Id == kvp.Key))
                .Where(q => q != null)
                .ToList();

            if (struggledQuestions.Any())
            {
                var reviewPanel = new StackPanel { Margin = new Thickness(18) };

                var titleBlock = new TextBlock
                {
                    Text = "📊 Review Answers",
                    Style = FindResource("QuestionTextStyle") as Style,
                    FontSize = 25,
                    Margin = new Thickness(0, 0, 0, 10),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                reviewPanel.Children.Add(titleBlock);

                // Add percentage display
                var percentageBlock = new TextBlock
                {
                    Text = $"{(correctCount + incorrectCount > 0 ? (int)Math.Floor((double)correctCount / (correctCount + incorrectCount) * 100) : 0)}%",
                    Style = FindResource("QuestionTextStyle") as Style,
                    FontSize = 29, // 4 bigger than titleBlock
                    Margin = new Thickness(0, 0, 0, 45),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                reviewPanel.Children.Add(percentageBlock);

                var grid = new Grid();
                int maxCols = 2;
                int maxRows = 4;

                for (int i = 0; i < maxRows; i++)
                    grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                for (int i = 0; i < maxCols; i++)
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                for (int i = 0; i < struggledQuestions.Count; i++)
                {
                    var question = struggledQuestions[i];
                    var mistakeCount = questionMistakeCount[question.Id];

                    var questionCard = new Border
                    {
                        Background = new LinearGradientBrush(Color.FromRgb(4, 102, 200), Color.FromRgb(3, 83, 164), 90),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(255, 0, 0)),
                        BorderThickness = new Thickness(5),
                        CornerRadius = new CornerRadius(12),
                        Padding = new Thickness(16),
                        Margin = new Thickness(8),
                        Effect = FindResource("CardShadow") as System.Windows.Media.Effects.DropShadowEffect
                    };

                    var cardContent = new StackPanel();

                    // Black rounded box for cross and missed text
                    var headerBox = new Border
                    {
                        Background = new SolidColorBrush(Colors.Black),
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(8, 4, 8, 4),
                        Margin = new Thickness(0, 0, 0, 8),
                        HorizontalAlignment = HorizontalAlignment.Left
                    };

                    var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
                    var errorIcon = new TextBlock { Text = "❌", FontSize = 16, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)) };
                    var mistakeText = new TextBlock
                    {
                        Text = $"Missed {mistakeCount} time{(mistakeCount > 1 ? "s" : "")}",
                        Foreground = Brushes.White,
                        FontSize = 14,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    headerPanel.Children.Add(errorIcon);
                    headerPanel.Children.Add(mistakeText);
                    headerBox.Child = headerPanel;
                    cardContent.Children.Add(headerBox);

                    var questionText = new TextBlock
                    {
                        Text = question.QuestionText?.Length > 100 ? question.QuestionText.Substring(0, 100) + "..." : question.QuestionText,
                        Foreground = Brushes.White,
                        FontWeight = FontWeights.Bold,
                        FontSize = 18,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 8)
                    };
                    cardContent.Children.Add(questionText);

                    var typeText = new TextBlock
                    {
                        Text = GetQuestionTypeDisplayName(question.Type),
                        Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                        FontSize = 14,
                        FontStyle = FontStyles.Italic
                    };
                    cardContent.Children.Add(typeText);

                    questionCard.Child = cardContent;
                    Grid.SetRow(questionCard, i / maxCols);
                    Grid.SetColumn(questionCard, i % maxCols);
                    grid.Children.Add(questionCard);
                }

                reviewPanel.Children.Add(grid);
                AnswerGrid.Children.Add(reviewPanel);
            }
            else
            {
                var reviewPanel = new StackPanel { Margin = new Thickness(18) };
                var noMistakesText = new TextBlock
                {
                    Text = "🎉 Perfect! You didn't struggle with any questions!",
                    Style = FindResource("QuestionTextStyle") as Style,
                    FontSize = 25,
                    Margin = new Thickness(0, 0, 0, 10),
                    HorizontalAlignment = HorizontalAlignment.Center,
                };
                AnswerGrid.Children.Add(noMistakesText);
            }
        }

        private string GetQuestionTypeDisplayName(string type)
        {
            return type switch
            {
                "multiple_choice" => "Multiple Choice",
                "text_input" => "Text Input",
                "true_false" => "True/False",
                "matching" => "Matching",
                "ordering" => "Ordering",
                _ => "Unknown"
            };
        }

        private (int rows, int cols) GetGridLayout(int itemCount)
        {
            return itemCount switch
            {
                2 => (1, 2),
                3 => (1, 3),
                4 => (2, 2),
                5 => (2, 3),
                6 => (2, 3),
                _ => (1, itemCount)
            };
        }

        private void LoadMultipleChoice()
        {
            if (currentQuestion.Answers == null || currentQuestion.GoodAnswers == null)
            {
                QuestionText.Text = "Invalid multiple choice question data.";
                SubmitButton.IsEnabled = false;
                return;
            }

            int answerCount = currentQuestion.Answers.Length;
            var (rows, cols) = GetGridLayout(answerCount);
            var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };

            if (answerCount == 5)
            {
                // Define two rows
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                // First row: 2 columns (1/2 - 1/2)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // Second row will use a nested grid with 3 columns
            }
            else
            {
                for (int i = 0; i < rows; i++) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                for (int i = 0; i < cols; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            var indices = Enumerable.Range(0, answerCount).OrderBy(x => random.Next()).ToList();
            multipleChoiceButtonStates.Clear();
            multipleChoiceButtonNumbers.Clear();

            if (answerCount == 5)
            {
                // First row: 2 buttons
                for (int i = 0; i < 2; i++)
                {
                    int originalIndex = indices[i];
                    var button = new Button
                    {
                        Content = currentQuestion.Answers[originalIndex],  // Keep this as string
                        Style = FindResource("MultipleChoiceButtonStyle") as Style,
                        Tag = (i + 1).ToString(),
                        Background = new SolidColorBrush(Color.FromRgb(2, 62, 125)),
                        Margin = new Thickness(5)
                    };
                    button.Click += MultipleChoiceButton_Click;
                    multipleChoiceButtonStates.Add(button, false);
                    multipleChoiceButtonNumbers.Add(button, originalIndex);

                    Grid.SetRow(button, 0);
                    Grid.SetColumn(button, i);
                    Grid.SetColumnSpan(button, 1);
                    grid.Children.Add(button);
                }

                // Second row: 3 buttons in a nested grid
                var nestedGrid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
                for (int i = 0; i < 3; i++)
                {
                    nestedGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                }
                nestedGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                for (int i = 2; i < 5; i++)
                {
                    int originalIndex = indices[i];
                    var button = new Button
                    {
                        Content = new TextBlock
                        {
                            Text = currentQuestion.Answers[originalIndex],
                            TextWrapping = TextWrapping.Wrap,
                            LineHeight = 25
                        },
                        Style = FindResource("MultipleChoiceButtonStyle") as Style,
                        Tag = (i + 1).ToString(),
                        Background = new SolidColorBrush(Color.FromRgb(2, 62, 125)),
                        Margin = new Thickness(5)
                    };
                    button.Click += MultipleChoiceButton_Click;
                    multipleChoiceButtonStates.Add(button, false);
                    multipleChoiceButtonNumbers.Add(button, originalIndex);

                    Grid.SetRow(button, 0);
                    Grid.SetColumn(button, i - 2);
                    Grid.SetColumnSpan(button, 1);
                    nestedGrid.Children.Add(button);
                }

                Grid.SetRow(nestedGrid, 1);
                Grid.SetColumn(nestedGrid, 0);
                Grid.SetColumnSpan(nestedGrid, 2);
                grid.Children.Add(nestedGrid);
            }
            else
            {
                for (int i = 0; i < answerCount; i++)
                {
                    int originalIndex = indices[i];
                    var button = new Button
                    {
                        Content = new TextBlock
                        {
                            Text = currentQuestion.Answers[originalIndex],
                            TextWrapping = TextWrapping.Wrap,
                            LineHeight = 25
                        },
                        Style = FindResource("MultipleChoiceButtonStyle") as Style,
                        Tag = (i + 1).ToString(),
                        Background = new SolidColorBrush(Color.FromRgb(2, 62, 125)),
                        Margin = new Thickness(5)
                    };
                    button.Click += MultipleChoiceButton_Click;  // ADD THIS LINE
                    multipleChoiceButtonStates.Add(button, false);
                    multipleChoiceButtonNumbers.Add(button, originalIndex);

                    Grid.SetRow(button, i / cols);
                    Grid.SetColumn(button, i % cols);
                    grid.Children.Add(button);
                }
            }
            AnswerGrid.Children.Add(grid);
        }

        private void MultipleChoiceButton_Click(object sender, RoutedEventArgs e)
        {
            if (isAnswerSubmitted) return;
            var button = sender as Button;
            bool isSelected = multipleChoiceButtonStates[button];
            multipleChoiceButtonStates[button] = !isSelected;
            button.Background = !isSelected
                ? new SolidColorBrush(Color.FromRgb(147, 197, 253))
                : new SolidColorBrush(Color.FromRgb(2, 62, 125));
            button.BorderThickness = !isSelected ? new Thickness(7) : new Thickness(1);
        }

        private void MultipleChoiceSubmit_Click(ref bool isCorrect)
        {
            var selectedIndices = multipleChoiceButtonStates
                .Where(kvp => kvp.Value)
                .Select(kvp => multipleChoiceButtonNumbers[kvp.Key])
                .ToList();

            isCorrect = selectedIndices.Count == currentQuestion.GoodAnswers.Length &&
                       selectedIndices.All(i => Array.IndexOf(currentQuestion.GoodAnswers, i) >= 0);

            foreach (var kvp in multipleChoiceButtonStates)
            {
                var button = kvp.Key;
                int index = multipleChoiceButtonNumbers[button];
                bool isGoodAnswer = Array.IndexOf(currentQuestion.GoodAnswers, index) >= 0;

                if (kvp.Value) // Selected by user - keep vibrant
                {
                    button.Background = isGoodAnswer ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Red);
                    button.BorderThickness = new Thickness(7);
                }
                else // Not selected - make dimmer
                {
                    button.Background = isGoodAnswer ?
                        new SolidColorBrush(Color.FromArgb(128, 0, 128, 0)) : // Dim green
                        new SolidColorBrush(Color.FromArgb(128, 255, 0, 0));   // Dim red
                    button.BorderThickness = new Thickness(1);
                }
            }
        }

        private void LoadTextInput()
        {
            if (currentQuestion.ExpectedAnswer == null)
            {
                QuestionText.Text = "Invalid text input question data.";
                SubmitButton.IsEnabled = false;
                return;
            }

            var stackPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            var textBox = new TextBox
            {
                Width = 300,
                Margin = new Thickness(10),
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromRgb(2, 62, 125)),
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                FontSize = 20,
                FontFamily = new FontFamily("Segoe UI"),
                Padding = new Thickness(5)
            };

            // Add Enter key handler for text input - FIXED to not go to next question
            textBox.KeyDown += (s, e) => {
                if (e.Key == Key.Enter && SubmitButton.IsEnabled && !isAnswerSubmitted)
                {
                    SubmitButton_Click(SubmitButton, new RoutedEventArgs());
                    e.Handled = true;
                }
            };

            stackPanel.Children.Add(textBox);

            correctAnswerTextBlock = new TextBlock
            {
                Style = FindResource("TextBlockStyle") as Style,
                Text = "",
                Visibility = Visibility.Hidden,
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            stackPanel.Children.Add(correctAnswerTextBlock);
            AnswerGrid.Children.Add(stackPanel);
            SubmitButton.Tag = textBox;
        }

        private void CheckTextInput(TextBox textBox, ref bool isCorrect)
        {
            isCorrect = IsCloseAnswer(textBox.Text?.Trim() ?? "", currentQuestion.ExpectedAnswer);
            textBox.Background = isCorrect ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Red);

            correctAnswerTextBlock.Text = $"Correct answer: {currentQuestion.ExpectedAnswer}";
            correctAnswerTextBlock.Visibility = Visibility.Visible;
        }

        private void LoadTrueFalse()
        {
            var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var trueButton = new Button
            {
                Content = "True",
                Style = FindResource("TrueFalseButtonStyle") as Style,
                Tag = "T",
                Background = new SolidColorBrush(Color.FromRgb(2, 62, 125))
            };
            var falseButton = new Button
            {
                Content = "False",
                Style = FindResource("TrueFalseButtonStyle") as Style,
                Tag = "F",
                Background = new SolidColorBrush(Color.FromRgb(2, 62, 125))
            };

            trueButton.Click += TrueFalseButton_Click;
            falseButton.Click += TrueFalseButton_Click;

            multipleChoiceButtonStates.Add(trueButton, false);
            multipleChoiceButtonStates.Add(falseButton, false);

            Grid.SetRow(trueButton, 0);
            Grid.SetColumn(trueButton, 0);
            Grid.SetRow(falseButton, 0);
            Grid.SetColumn(falseButton, 1);

            grid.Children.Add(trueButton);
            grid.Children.Add(falseButton);
            AnswerGrid.Children.Add(grid);
        }

        private void TrueFalseButton_Click(object sender, RoutedEventArgs e)
        {
            if (isAnswerSubmitted) return;
            var clickedButton = sender as Button;

            foreach (var kvp in multipleChoiceButtonStates.ToList())
            {
                var button = kvp.Key;
                multipleChoiceButtonStates[button] = false;
                button.Background = new SolidColorBrush(Color.FromRgb(2, 62, 125));
                button.BorderThickness = new Thickness(2);
            }

            multipleChoiceButtonStates[clickedButton] = true;
            clickedButton.Background = new SolidColorBrush(Color.FromRgb(147, 197, 253));
            clickedButton.BorderThickness = new Thickness(7);
        }

        private void CheckTrueFalse(ref bool isCorrect)
        {
            var selectedButton = multipleChoiceButtonStates.FirstOrDefault(kvp => kvp.Value).Key;

            if (selectedButton == null)
            {
                isCorrect = false;
                // Color both buttons to show correct answer even if nothing was selected
                foreach (var kvp in multipleChoiceButtonStates)
                {
                    var button = kvp.Key;
                    bool isCorrectButton = (button.Content.ToString() == "True") == currentQuestion.CorrectAnswer;
                    button.Background = isCorrectButton ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Red);
                }
                return;
            }

            bool selected = selectedButton.Content.ToString() == "True";
            isCorrect = selected == currentQuestion.CorrectAnswer;

            // Color both buttons appropriately
            foreach (var kvp in multipleChoiceButtonStates)
            {
                var button = kvp.Key;
                bool isCorrectButton = (button.Content.ToString() == "True") == currentQuestion.CorrectAnswer;

                if (kvp.Value) // Selected by user - keep vibrant
                {
                    button.Background = isCorrectButton ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Red);
                }
                else // Not selected - make dimmer
                {
                    button.Background = isCorrectButton ?
                        new SolidColorBrush(Color.FromArgb(128, 0, 128, 0)) : // Dim green
                        new SolidColorBrush(Color.FromArgb(128, 255, 0, 0));   // Dim red
                }
            }
        }

        private void LoadMatching()
        {
            if (currentQuestion.Pairs == null)
            {
                QuestionText.Text = "Invalid matching question data.";
                SubmitButton.IsEnabled = false;
                return;
            }

            int pairCount = currentQuestion.Pairs.Length;
            var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
            for (int i = 0; i < pairCount; i++) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var leftItems = currentQuestion.Pairs.Select(p => p.Left).ToList();
            var rightItems = currentQuestion.Pairs.Select(p => p.Right).OrderBy(x => random.Next()).ToList();

            movableBoxes.Clear();

            for (int i = 0; i < pairCount; i++)
            {
                var leftBorder = CreateMovableBox(leftItems[i], false, (i + 1).ToString(), 590);
                var rightBorder = CreateMovableBox(rightItems[i], true, ((char)('A' + i)).ToString(), 590);
                leftBorder.Tag = leftItems[i]; // Store the original left item for matching
                rightBorder.Tag = rightItems[i]; // Store the original right item for matching
                Grid.SetRow(leftBorder, i);
                Grid.SetColumn(leftBorder, 0);
                Grid.SetRow(rightBorder, i);
                Grid.SetColumn(rightBorder, 1);
                grid.Children.Add(leftBorder);
                grid.Children.Add(rightBorder);
                if (!movableBoxes.Contains(rightBorder))
                {
                    movableBoxes.Add(rightBorder);
                }
            }
            AnswerGrid.Children.Add(grid);
        }

        private void CheckMatching(ref bool isCorrect)
        {
            var grid = AnswerGrid.Children[0] as Grid;
            if (grid == null) return;

            isCorrect = true;
            for (int i = 0; i < currentQuestion.Pairs.Length; i++)
            {
                var leftBox = grid.Children.Cast<UIElement>()
                    .FirstOrDefault(child => Grid.GetRow(child) == i && Grid.GetColumn(child) == 0) as Border;
                var rightBox = grid.Children.Cast<UIElement>()
                    .FirstOrDefault(child => Grid.GetRow(child) == i && Grid.GetColumn(child) == 1) as Border;

                if (leftBox == null || rightBox == null)
                {
                    isCorrect = false;
                    continue;
                }

                string leftText = (leftBox.Child as StackPanel)?.Children.OfType<TextBlock>().FirstOrDefault()?.Text;
                string rightText = (rightBox.Child as StackPanel)?.Children.OfType<TextBlock>().FirstOrDefault()?.Text;

                if (string.IsNullOrEmpty(leftText) || string.IsNullOrEmpty(rightText))
                {
                    isCorrect = false;
                    leftBox.Background = new SolidColorBrush(Colors.Red);
                    rightBox.Background = new SolidColorBrush(Colors.Red);
                    continue;
                }

                // Find the correct right item for this left item
                var correctPair = currentQuestion.Pairs.FirstOrDefault(p => p.Left == leftText);
                if (correctPair == null)
                {
                    isCorrect = false;
                    leftBox.Background = new SolidColorBrush(Colors.Red);
                    rightBox.Background = new SolidColorBrush(Colors.Red);
                    continue;
                }

                bool matchCorrect = correctPair.Right == rightText;
                leftBox.Background = matchCorrect ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Red);
                rightBox.Background = matchCorrect ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Red);
                if (!matchCorrect) isCorrect = false;
            }
        }

        private void LoadOrdering()
        {
            if (currentQuestion.Items == null || currentQuestion.CorrectOrder == null)
            {
                QuestionText.Text = "Invalid ordering question data.";
                SubmitButton.IsEnabled = false;
                return;
            }

            int itemCount = currentQuestion.Items.Length;
            var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
            for (int i = 0; i < itemCount; i++) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // DON'T shuffle - use original order or shuffle indices instead
            var shuffledIndices = Enumerable.Range(0, itemCount).OrderBy(x => random.Next()).ToList();

            movableBoxes.Clear();

            for (int i = 0; i < itemCount; i++)
            {
                int itemIndex = shuffledIndices[i];
                var border = CreateMovableBox(currentQuestion.Items[itemIndex], true, ((char)('A' + i)).ToString(), 1250);
                border.Tag = itemIndex; // Store the original index, not the position
                Grid.SetRow(border, i);
                Grid.SetColumn(border, 0);
                grid.Children.Add(border);
                if (!movableBoxes.Contains(border))
                {
                    movableBoxes.Add(border);
                }
            }
            AnswerGrid.Children.Add(grid);
        }

        private void CheckOrdering(ref bool isCorrect)
        {
            var grid = AnswerGrid.Children[0] as Grid;
            if (grid == null)
            {
                isCorrect = false;
                return;
            }

            // Get user's current order (based on Tag, which stores the original item index)
            var userOrderIndices = new List<int>();
            var borders = new List<Border>();

            for (int row = 0; row < grid.RowDefinitions.Count; row++)
            {
                var border = grid.Children.Cast<Border>().FirstOrDefault(b => Grid.GetRow(b) == row);
                if (border != null && border.Tag is int itemIndex)
                {
                    borders.Add(border);
                    userOrderIndices.Add(itemIndex);
                }
                else
                {
                    // If a border is missing or Tag is invalid, mark as incorrect
                    isCorrect = false;
                    return;
                }
            }

            // Compare user order indices with CorrectOrder
            isCorrect = true;
            for (int i = 0; i < userOrderIndices.Count && i < currentQuestion.CorrectOrder.Length; i++)
            {
                bool itemCorrect = userOrderIndices[i] == currentQuestion.CorrectOrder[i];
                borders[i].Background = itemCorrect ?
                    new SolidColorBrush(Colors.Green) :
                    new SolidColorBrush(Colors.Red);

                if (!itemCorrect) isCorrect = false;
            }

            // If lengths don't match, the order is incorrect
            if (userOrderIndices.Count != currentQuestion.CorrectOrder.Length)
            {
                isCorrect = false;
            }
        }

        private Border CreateMovableBox(string text, bool isMovable, string numberLabel, double maxWidth = 300)
        {
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };

            var numberBorder = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(Color.FromRgb(4, 102, 200)),
                Margin = new Thickness(0, 0, 12, 0),
                Child = new TextBlock
                {
                    Text = numberLabel,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = FontWeights.Bold,
                    FontSize = 12,
                    Foreground = Brushes.White
                }
            };

            var textBlock = new TextBlock
            {
                Text = text,
                Foreground = Brushes.White,
                FontSize = 18,
                FontFamily = new FontFamily("Segoe UI"),
                FontWeight = FontWeights.Medium,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = maxWidth,  // Use the parameter
                LineHeight = 25  // Add this for slightly bigger line spacing
            };

            stackPanel.Children.Add(numberBorder);
            stackPanel.Children.Add(textBlock);

            var border = new Border
            {
                Background = new LinearGradientBrush(Color.FromRgb(2, 62, 125), Color.FromRgb(0, 40, 85), 90),
                BorderBrush = new SolidColorBrush(Color.FromRgb(4, 102, 200)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(4),
                Child = stackPanel,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Effect = FindResource("ButtonShadow") as System.Windows.Media.Effects.DropShadowEffect
            };

            if (isMovable)
            {
                border.MouseLeftButtonDown += Border_MouseLeftButtonDown;
                border.MouseMove += Border_MouseMove;
                border.MouseLeftButtonUp += Border_MouseLeftButtonUp;
                border.Cursor = Cursors.Hand;
            }

            return border;
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border == null) return;
            dragStartPoints[border] = e.GetPosition(AnswerGrid);
            border.CaptureMouse();
        }

        private void Border_MouseMove(object sender, MouseEventArgs e)
        {
            var border = sender as Border;
            if (border == null || !border.IsMouseCaptured) return;

            var currentPos = e.GetPosition(AnswerGrid);
            var grid = VisualTreeHelper.GetParent(border) as Grid;
            if (grid == null) return;

            int currentRow = Grid.GetRow(border);
            int currentCol = Grid.GetColumn(border);
            int newRow = currentRow;
            double minDistance = double.MaxValue;

            foreach (var otherBorder in movableBoxes)
            {
                if (otherBorder == border || Grid.GetColumn(otherBorder) != currentCol) continue;

                var otherPos = otherBorder.TranslatePoint(new Point(0, 0), AnswerGrid);
                double distance = currentPos.Y - otherPos.Y;
                if (Math.Abs(distance) < 50 && Math.Abs(distance) < minDistance)
                {
                    newRow = Grid.GetRow(otherBorder);
                    minDistance = Math.Abs(distance);
                }
            }

            if (newRow != currentRow)
            {
                Grid.SetRow(border, newRow);
                foreach (var otherBorder in movableBoxes)
                {
                    if (otherBorder == border || Grid.GetColumn(otherBorder) != currentCol) continue;
                    int otherRow = Grid.GetRow(otherBorder);
                    if (otherRow == newRow && otherBorder != border)
                    {
                        Grid.SetRow(otherBorder, currentRow);
                        break;
                    }
                }

                var orderedBorders = grid.Children.Cast<Border>()
                    .Where(b => Grid.GetColumn(b) == currentCol)
                    .OrderBy(b => Grid.GetRow(b))
                    .ToList();
                for (int i = 0; i < orderedBorders.Count; i++)
                {
                    Grid.SetRow(orderedBorders[i], i);
                }
            }
        }

        private void Border_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border == null) return;

            border.ReleaseMouseCapture();
            dragStartPoints.Remove(border);

            var grid = VisualTreeHelper.GetParent(border) as Grid;
            if (grid == null) return;

            var currentCol = Grid.GetColumn(border);
            var orderedBorders = grid.Children.Cast<Border>()
                .Where(b => Grid.GetColumn(b) == currentCol)
                .OrderBy(b => Grid.GetRow(b))
                .ToList();

            for (int i = 0; i < orderedBorders.Count; i++)
            {
                Grid.SetRow(orderedBorders[i], i);
            }
        }

        private void UpdateScore(bool isCorrect)
        {
            if (isCorrect) correctCount++;
            else incorrectCount++;

            CorrectCount.Text = correctCount.ToString();
            IncorrectCount.Text = incorrectCount.ToString();


            // Calculate percentage (floor to int)
            int totalQuestions = correctCount + incorrectCount;
            int percentage = totalQuestions > 0 ? (int)Math.Floor((double)correctCount / totalQuestions * 100) : 0;
            UpdateQuestionCount(correctCount);
        }

        private void UpdateQuestionCount(int correctCount)
        {
            int allQuesitonCount = questions.Count - correctCount;
            TaskCount.Text = allQuesitonCount.ToString();
        }

        private void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            if (isAnswerSubmitted) return;
            isAnswerSubmitted = true;
            SubmitButton.IsEnabled = false;
            NextQuestionButton.IsEnabled = true;

            bool isCorrect = true;
            bool wasAnswered = true; // Track if user actually answered

            switch (currentQuestion.Type)
            {
                case "multiple_choice":
                    // Check if any answer was selected
                    wasAnswered = multipleChoiceButtonStates.Any(kvp => kvp.Value);
                    MultipleChoiceSubmit_Click(ref isCorrect);
                    break;
                case "text_input":
                    var textBox = SubmitButton.Tag as TextBox;
                    if (textBox != null)
                    {
                        wasAnswered = !string.IsNullOrWhiteSpace(textBox.Text);
                        CheckTextInput(textBox, ref isCorrect);
                    }
                    break;
                case "true_false":
                    // Check if any answer was selected
                    wasAnswered = multipleChoiceButtonStates.Any(kvp => kvp.Value);
                    CheckTrueFalse(ref isCorrect);
                    break;
                case "matching":
                    CheckMatching(ref isCorrect);
                    break;
                case "ordering":
                    CheckOrdering(ref isCorrect);
                    break;
            }

            AnswerIndicator.Text = isCorrect ? "Correct" : "Incorrect";
            IndicatorBorder.Background = isCorrect ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Red);
            IndicatorIcon.Text = isCorrect ? "✓" : "✗";
            AnswerIndicator.Visibility = Visibility.Visible;
            IndicatorBorder.Visibility = Visibility.Visible;

            if (!isCorrect || !wasAnswered)
            {
                if (!incorrectQuestionIds.Contains(currentQuestion.Id))
                    incorrectQuestionIds.Add(currentQuestion.Id);

                // Track mistake count for review (both wrong answers and unanswered)
                if (questionMistakeCount.ContainsKey(currentQuestion.Id))
                    questionMistakeCount[currentQuestion.Id]++;
                else
                    questionMistakeCount[currentQuestion.Id] = 1;
            }
            else
            {
                // Remove from incorrect list if answered correctly
                incorrectQuestionIds.Remove(currentQuestion.Id);
            }
            UpdateScore(isCorrect);
        }

        private void RestartButton_Click(object sender, RoutedEventArgs e)
        {
            correctCount = 0;
            incorrectCount = 0;
            CorrectCount.Text = "0";
            IncorrectCount.Text = "0";
            askedQuestionIds.Clear();
            incorrectQuestionIds.Clear();
            isRevisionMode = false;
            ModeText.Text = "Learning";
            Dispatcher.Invoke(() => ModeText.InvalidateVisual());
            System.Diagnostics.Debug.WriteLine("Restarted, ModeText set to 'Learning'");
            AnswerIndicator.Visibility = Visibility.Hidden;
            IndicatorBorder.Visibility = Visibility.Hidden;
            questionOrder.Clear();
            questionMistakeCount.Clear();
            LoadRandomQuestion();
        }

        private void QuitButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void NextQuestionButton_Click(object sender, RoutedEventArgs e)
        {
            LoadRandomQuestion();
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            var helpWindow = new HelpWindow();
            helpWindow.Owner = this;
            helpWindow.ShowDialog();
        }

        private void LearnButton_Click(object sender, RoutedEventArgs e)
        {
            var learnWindow = new LearnWindow(questions);
            learnWindow.Owner = this;
            learnWindow.ShowDialog();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Don't handle space/enter if text input has focus
            if (Keyboard.FocusedElement is TextBox)
                return;

            // Handle Space and Enter for Submit/Next Question
            if ((e.Key == Key.Space || e.Key == Key.Enter) && !isAnswerSubmitted && SubmitButton.IsEnabled)
            {
                SubmitButton_Click(SubmitButton, new RoutedEventArgs());
                e.Handled = true;
            }
            else if ((e.Key == Key.Space || e.Key == Key.Enter) && isAnswerSubmitted && NextQuestionButton.IsEnabled)
            {
                NextQuestionButton_Click(NextQuestionButton, new RoutedEventArgs());
                e.Handled = true;
            }
            // Handle number keys for multiple choice selection
            else if (currentQuestion?.Type == "multiple_choice" && !isAnswerSubmitted)
            {
                int number = -1;
                if (e.Key >= Key.D1 && e.Key <= Key.D9)
                {
                    number = e.Key - Key.D0;
                }
                else if (e.Key >= Key.NumPad1 && e.Key <= Key.NumPad9)
                {
                    number = e.Key - Key.NumPad0;
                }

                if (number > 0)
                {
                    var buttons = multipleChoiceButtonStates.Keys.ToList();
                    var targetButton = buttons.FirstOrDefault(b => b.Tag.ToString() == number.ToString());
                    if (targetButton != null)
                    {
                        MultipleChoiceButton_Click(targetButton, new RoutedEventArgs());
                        e.Handled = true;
                    }
                }
            }
            // Handle T and F keys for true/false selection
            else if (currentQuestion?.Type == "true_false" && !isAnswerSubmitted)
            {
                if (e.Key == Key.T)
                {
                    var buttons = multipleChoiceButtonStates.Keys.ToList();
                    var trueButton = buttons.FirstOrDefault(b => b.Content.ToString() == "True");
                    if (trueButton != null)
                    {
                        TrueFalseButton_Click(trueButton, new RoutedEventArgs());
                        e.Handled = true;
                    }
                }
                else if (e.Key == Key.F)
                {
                    var buttons = multipleChoiceButtonStates.Keys.ToList();
                    var falseButton = buttons.FirstOrDefault(b => b.Content.ToString() == "False");
                    if (falseButton != null)
                    {
                        TrueFalseButton_Click(falseButton, new RoutedEventArgs());
                        e.Handled = true;
                    }
                }
            }
            // Handle R for Restart
            else if (e.Key == Key.R && RestartButton.IsEnabled)
            {
                RestartButton_Click(RestartButton, new RoutedEventArgs());
                e.Handled = true;
            }
            // Handle Escape for Quit
            else if (e.Key == Key.Escape)
            {
                QuitButton_Click(QuitButton, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        public static bool IsCloseAnswer(string input, string expected)
        {
            input = input.Trim();
            expected = expected.Trim();

            if (IsNumeric(expected))
            {
                return input == expected;
            }

            input = Normalize(input);
            expected = Normalize(expected);

            int distance = LevenshteinDistance(input, expected);
            int maxLen = Math.Max(input.Length, expected.Length);

            return distance <= Math.Max(1, maxLen / 5);
        }

        private static string Normalize(string s)
        {
            return s.ToLower().Replace(" ", "");
        }

        private static bool IsNumeric(string s)
        {
            foreach (char c in s)
            {
                if (c < '0' || c > '9')
                    return false;
            }
            return s.Length > 0;
        }

        private static int LevenshteinDistance(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1,     // deletion
                                 d[i, j - 1] + 1),    // insertion
                                 d[i - 1, j - 1] + cost); // substitution
                }
            }

            return d[n, m];
        }
    }

    public class Question
    {
        public string Id { get; set; }
        public string Type { get; set; }
        [JsonProperty("question")]
        public string QuestionText { get; set; }
        public string[] Answers { get; set; }
        [JsonProperty("good_answers")]
        public int[] GoodAnswers { get; set; }
        [JsonProperty("expected_answer")]
        public string ExpectedAnswer { get; set; }
        [JsonProperty("correct_answer")]
        public bool CorrectAnswer { get; set; }
        public Pair[] Pairs { get; set; }
        public string[] Items { get; set; }
        [JsonProperty("correct_order")]
        public int[] CorrectOrder { get; set; }
    }

    public class Pair
    {
        public string Left { get; set; }
        public string Right { get; set; }
    }
}