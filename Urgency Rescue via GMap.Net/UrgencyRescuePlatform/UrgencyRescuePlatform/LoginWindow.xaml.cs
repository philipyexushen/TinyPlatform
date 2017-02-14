using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using IpHelperSpace;

namespace UrgencyRescuePlatform
{
	/// <summary>
	/// LoginWindow.xaml 的交互逻辑
	/// </summary>
	public partial class LoginWindow : Window
	{
		public string HostName { get; private set; }
		public ushort Port { get; private set; }
		public string UserName { get; private set; }
		public string Ip { get; private set; }

		private bool _searchCompleted = false;
		private bool _loginButtonClick = false;

		public LoginWindow()
		{
			InitializeComponent();
			_button_login.IsEnabled = false;
		}

		private void win_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			this.DragMove();
		}

		private void closeButton_Click(object sender, RoutedEventArgs e)
		{
			this.Close();
		}

		private void _button_login_Click(object sender, RoutedEventArgs e)
		{
			HostName = _textbox_addressEditor.Text;
			Port     = ushort.Parse(_textbox_portEditor.Text);
			UserName = _textbox_userName.Text;
			Ip       = _textbox_ip.Text;

			_loginButtonClick = true;
			this.DialogResult = true;
		}

		private void login_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			if (canLogin())
			{
				e.CanExecute = _button_login.IsEnabled = true;
			}
			else
			{
				e.CanExecute = _button_login.IsEnabled = false;
			}
		}

		private void login_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			if (!_loginButtonClick)
				_button_login_Click(sender, e);
		}

		private void _textbox_portEditor_Pasting(object sender, DataObjectPastingEventArgs e)
		{
			if (e.DataObject.GetDataPresent(typeof(string)))
			{
				string text = (string)e.DataObject.GetData(typeof(string));
				if (!isNumberic(text))
					e.CancelCommand();
			}
			else
				e.CancelCommand();
		}

		private void _textbox_portEditor_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Space)
				e.Handled = true;
		}

		private void _textbox_portEditor_PreviewTextInput(object sender, TextCompositionEventArgs e)
		{
			if (!isNumberic(e.Text))
			{
				e.Handled = true;
			}
			else
				e.Handled = false;
		}

		private static bool isNumberic(string s)
		{
			if (string.IsNullOrEmpty(s))
				return false;
			foreach (char c in s)
			{
				if (! char.IsDigit(c))
					return false;
			}
			return true;
		}

		private void _button_searchIp_Click(object sender, RoutedEventArgs e)
		{
			if (!IpHelper.IsRequery)
			{
				_textbox_ip.Text = "正在查询...";
				IpHelper.GetIpAndCoordinateAsync(async_getIpHandler);
			}

			FocusManager.SetFocusedElement(this, this);
		}

		private void async_getIpHandler(IpDetails details)
		{
			Application.Current.Dispatcher.Invoke(
				new Action(() => 
				{
					if (details.Status == IpDetails.DetailsStatus.Successful)
					{
						_textbox_ip.Text = details.Ip;
						_searchCompleted = true;
					}
					else
					{
						_textbox_ip.Text = "查询失败";
						_searchCompleted = false;
					}
				}
				));
		}

		private void _textboxes_TextChanged(object sender, TextChangedEventArgs e)
		{
			//强制CanExcute
			CommandManager.InvalidateRequerySuggested();
		}

		private void _textbox_portEditor_TextChanged(object sender, TextChangedEventArgs e)
		{
			TextBox textBox = sender as TextBox;
			TextChange[] change = new TextChange[e.Changes.Count];
			e.Changes.CopyTo(change, 0);

			int offset = change[0].Offset;
			if (change[0].AddedLength > 0)
			{
				ushort num = 0;
				if (!ushort.TryParse(textBox.Text, out num))
				{
					textBox.Text = textBox.Text.Remove(offset, change[0].AddedLength);
					textBox.Select(offset, 0);
				}
			}
			_textboxes_TextChanged(sender, e);
		}

		private bool canLogin()
		{
			return _searchCompleted
				&& _textbox_portEditor.Text.Length != 0
				&& _textbox_userName.Text.Length != 0
				&& _textbox_addressEditor.Text.Length != 0;
		}
	}

	public class LoginCommand
	{
		private static RoutedUICommand login;

		static LoginCommand()
		{
			InputGestureCollection inputs = new InputGestureCollection();
			inputs.Add(new KeyGesture(Key.T, ModifierKeys.Control, "Ctrl + T"));
			login = new RoutedUICommand("Login", "Login", typeof(LoginCommand), inputs);
		}

		public static RoutedUICommand Login
		{
			get { return login; }
		}
	}
}
