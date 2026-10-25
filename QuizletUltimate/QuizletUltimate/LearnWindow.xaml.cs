using System.Collections;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Data;

namespace QuizletUltimate
{
    public partial class LearnWindow : Window
    {
        public LearnWindow(List<Question> questions = null)
        {
            InitializeComponent();
            var viewModels = questions?.ConvertAll(q => new QuestionViewModel(q)) ?? new List<QuestionViewModel>();
            DataContext = viewModels;
            System.Diagnostics.Debug.WriteLine($"DataContext set to List with {viewModels.Count} items");
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public class QuestionViewModel
    {
        public string Id { get; }
        public string Type { get; }
        public string QuestionText { get; }
        public List<AnswerViewModel> Answers { get; }
        public string ExpectedAnswer { get; }
        public bool CorrectAnswer { get; }
        public List<Pair> Pairs { get; }
        public List<ItemViewModel> OrderedItems { get; }

        public QuestionViewModel(Question question)
        {
            Id = question.Id;
            Type = question.Type;
            QuestionText = question.QuestionText;

            if (question.Type == "multiple_choice" && question.Answers != null && question.GoodAnswers != null)
            {
                Answers = new List<AnswerViewModel>();
                for (int i = 0; i < question.Answers.Length; i++)
                {
                    Answers.Add(new AnswerViewModel
                    {
                        Index = i,
                        Text = question.Answers[i],
                        IsCorrect = question.GoodAnswers.Contains(i)
                    });
                }
            }

            ExpectedAnswer = question.ExpectedAnswer;
            CorrectAnswer = question.CorrectAnswer;
            Pairs = question.Pairs != null ? new List<Pair>(question.Pairs) : null;

            if (question.Type == "ordering" && question.Items != null && question.CorrectOrder != null)
            {
                OrderedItems = new List<ItemViewModel>();
                for (int i = 0; i < question.CorrectOrder.Length; i++)
                {
                    int index = question.CorrectOrder[i];
                    OrderedItems.Add(new ItemViewModel
                    {
                        Index = i,
                        Text = question.Items[index]
                    });
                }
            }
        }
    }

    public class AnswerViewModel
    {
        public int Index { get; set; }
        public string Text { get; set; }
        public bool IsCorrect { get; set; }
    }

    public class ItemViewModel
    {
        public int Index { get; set; }
        public string Text { get; set; }
    }

    public class QuestionTypeConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value switch
            {
                "multiple_choice" => "Multiple Choice",
                "text_input" => "Text Input",
                "true_false" => "True/False",
                "matching" => "Matching",
                "ordering" => "Ordering",
                _ => "Unknown"
            };
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new System.NotImplementedException();
        }
    }

    public class IndexToLetterConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is int index)
            {
                return ((char)('A' + index)).ToString() + ".";
            }
            return string.Empty;
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new System.NotImplementedException();
        }
    }

    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is string type && parameter is string paramType)
            {
                return type == paramType ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new System.NotImplementedException();
        }
    }

    public class BooleanToStyleConverter : IMultiValueConverter
    {
        public object Convert(object[] values, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (values.Length == 3 && values[0] is bool isCorrect && values[1] is Style correctStyle && values[2] is Style defaultStyle)
            {
                return isCorrect ? correctStyle : defaultStyle;
            }
            return null;
        }

        public object[] ConvertBack(object value, System.Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new System.NotImplementedException();
        }
    }

    public class IsEmptyConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool isEmpty;
            if (value == null)
            {
                System.Diagnostics.Debug.WriteLine("DataContext is null");
                isEmpty = true;
            }
            else if (!(value is IList list))
            {
                System.Diagnostics.Debug.WriteLine($"DataContext is not an IList, type: {value.GetType()}");
                isEmpty = true; // Treat non-IList as empty to show the empty state
            }
            else
            {
                isEmpty = list.Count == 0;
                System.Diagnostics.Debug.WriteLine($"List count: {list.Count}, IsEmpty: {isEmpty}");
            }

            string param = parameter as string ?? string.Empty;
            System.Diagnostics.Debug.WriteLine($"Parameter: {param}, IsEmpty: {isEmpty}");

            if (param == "VisibleWhenEmpty")
            {
                return isEmpty ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (param == "HiddenWhenEmpty")
            {
                return isEmpty ? Visibility.Collapsed : Visibility.Visible;
            }

            return isEmpty; // Fallback for other uses
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new System.NotImplementedException();
        }
    }
}