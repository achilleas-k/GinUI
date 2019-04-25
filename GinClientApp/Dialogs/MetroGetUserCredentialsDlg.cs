﻿using System;
using System.Linq;
using System.Windows.Forms;
using GinClientApp.Properties;
using MetroFramework.Forms;

namespace GinClientApp.Dialogs
{
    public partial class MetroGetUserCredentialsDlg : MetroForm
    {
        private readonly GinApplicationContext _parentContext;

        public MetroGetUserCredentialsDlg(GinApplicationContext parentContext)
        {
            InitializeComponent();
            //string serverJson = _parentContext.ServiceClient.GetServers();
            metroLabel1.TabStop = false;
            metroLabel2.TabStop = false;

            mLblWarning.Visible = false;
            mCBxServerAlias.Text = "gin";

            _parentContext = parentContext;

            mTxBUsername.Text = UserCredentials.Instance.loginList.First().Username;
            mTxBPassword.Text = UserCredentials.Instance.loginList.First().Password;
            mCBxServerAlias.Text = UserCredentials.Instance.loginList.First().Server;
        }

        private bool AttemptLogin()
        {
            if (string.IsNullOrEmpty(mTxBUsername.Text) || string.IsNullOrEmpty(mTxBPassword.Text) || string.IsNullOrEmpty(mCBxServerAlias.Text)) return false;

            _parentContext.ServiceClient.Logout();

            return _parentContext.ServiceClient.Login(mTxBUsername.Text, mTxBPassword.Text, mCBxServerAlias.Text);
            //return _parentContext.ServiceClient.Login(mTxBUsername.Text, mTxBPassword.Text);
        }

        private void mBtnOk_Click(object sender, EventArgs e)
        {
            mLblWarning.Visible = false;

            if (AttemptLogin())
            {
                
                var login = UserCredentials.Instance.loginList.Find(x => x.Server == mCBxServerAlias.SelectedText);
                if (login == null) {
                    login = (new UserCredentials.LoginSettings());
                    UserCredentials.Instance.loginList.Add(login);
                }
                login.Username = mTxBUsername.Text;
                login.Password = mTxBPassword.Text;
                login.Server = mCBxServerAlias.Text;
                MessageBox.Show(" usr "+ login.Username + " srv "+ login.Server);
                UserCredentials.Save();

                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                mLblWarning.Text = Resources.GetUserCredentials_The_entered_Username_Password_combination_is_invalid;
                mLblWarning.Visible = true;
            }
        }

        private void mBtnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}