using INSM.Server.Plugin.Framework.v1;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FileBasedUserStoreServerPlugin
{
    /// <summary>
    /// Example of an file based user store server plugin
    /// 
    /// Note that in this example a simpel SHA512 hash is used as password hash.
    /// For a production environment a more secure method should be used.
    /// </summary>
    public class FilebasedUserStoreServerPlugin : IUserStoreServerPlugin
    {
        private const string _UsersFilename = @"Users.txt";
        private const string _GroupsFilename = @"Groups.txt";

        private IUserStoreServerPluginContext _Context;
        private Dictionary<string, UserInfo> _Users;
        private Dictionary<string, GroupInfo> _Groups;
        private object _Lock = new object();

        public IUserStoreServerPluginContext Context
        {
            get { return _Context; }
            set { _Context = value; }
        }

        public int RequiredPlatformAPILevel
        {
            get { return 1; }
        }

        public string Name
        {
            get { return "File based user storage"; }
        }

        public string Vendor
        {
            get { return "INSM"; }
        }

        public string Version
        {
            get { return "1.0"; }
        }

        public GroupsAndUsers Reload(string userGroup, string adminGroup, string playerGroup, string domain)
        {
            lock (_Lock)
            {
                GroupsAndUsers groupsAndUsers = new GroupsAndUsers();

                _Users = ReadUsers();
                _Groups = ReadGroups();
                Dictionary<string, GroupTree> groupTrees = OrganizeGroups();

                List<string> users = new List<string>();
                List<string> admins = new List<string>();
                List<string> players = new List<string>();

                if (groupTrees.ContainsKey(userGroup))
                {
                    ExtractUsers(groupTrees[userGroup], users);
                }
                if (groupTrees.ContainsKey(adminGroup))
                {
                    ExtractUsers(groupTrees[adminGroup], admins);
                }
                if (groupTrees.ContainsKey(playerGroup))
                {
                    ExtractUsers(groupTrees[playerGroup], players);
                }

                foreach (UserInfo userInfo in _Users.Values)
                {
                    User user = userInfo.User;
                    if (string.IsNullOrEmpty(user.Domain) || user.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase))
                    {
                        user.IsAdmin = admins.Contains(user.Username);
                        user.IsPlayer = players.Contains(user.Username);
                        bool isUser = users.Contains(user.Username);
                        if (user.IsPlayer || user.IsAdmin || isUser)
                        {
                            groupsAndUsers.Users.Add(user);
                        }
                    }
                }
                foreach (GroupInfo groupInfo in _Groups.Values)
                {
                    groupsAndUsers.Groups.Add(groupInfo.Group);
                }

                return groupsAndUsers;
            }
        }

        private void ExtractUsers(GroupTree groupTree, List<string> users)
        {
            foreach (UserInfo user in _Users.Values)            
            {
                if (IsUserInGroupTree(user, groupTree))
                {
                    users.Add(user.User.Username);
                }
            }
        }

        private bool IsUserInGroupTree(UserInfo user, GroupTree groupTree)
        {
            if (_Groups.ContainsKey(groupTree.Groupname))
            {
                GroupInfo group = _Groups[groupTree.Groupname];
                if (user.Groupnames.Contains(group.Group.Groupname))
                {
                    //User belongs to group
                    if (!group.Group.Usernames.Contains(user.User.Username))
                    {
                        group.Group.Usernames.Add(user.User.Username);
                    }
                    return true;
                }
                foreach (GroupTree g in groupTree.SubGroups)
                {
                    return IsUserInGroupTree(user, g);
                }
            }
            return false;
        }

        public bool LogonUser(string username, string password, string domain, out bool isPlayerUser)
        {
            lock (_Lock)
            {
                isPlayerUser = false;
                if (_Users.ContainsKey(username))
                {
                    if (_Users[username].PasswordHash == ComputeHash(password))
                    {
                        isPlayerUser = _Users[username].User.IsPlayer;
                        return true;
                    }
                }
                return false;
            }
        }

        public void AddUser(User userInfo, string password, string groupname)
        {
            if (userInfo == null)
            {
                throw new ArgumentException("No information provided on add user");
            }
            lock (_Lock)
            {
                if (_Users.ContainsKey(userInfo.Username))
                {
                    throw new ArgumentException("User " + userInfo.Username + " is already created");
                }
                _Users.Add(userInfo.Username, new UserInfo()
                {
                    User = userInfo,
                    PasswordHash = ComputeHash(password)
                });
                WriteUsers();
            }
        }

        public void UpdateUser(User userInfo, string password)
        {
            if (userInfo == null)
            {
                throw new ArgumentException("No information provided on update user");
            }
            lock (_Lock)
            {
                if (!_Users.ContainsKey(userInfo.Username))
                {
                    throw new ArgumentException("User " + userInfo.Username + " does not exist on update user");
                }
                _Users[userInfo.Username].User = userInfo;
                _Users[userInfo.Username].PasswordHash = ComputeHash(password);
                WriteUsers();
            }
        }

        public void DeleteUser(string username)
        {
            if (username == null)
            {
                throw new ArgumentException("No information provided on delete user");
            }
            lock (_Lock)
            {
                if (!_Users.ContainsKey(username))
                {
                    throw new ArgumentException("User " + username + " does not exist on delete user");
                }
                _Users.Remove(username);
                WriteUsers();
            }
        }

        public void AddGroup(Group groupInfo)
        {
            if (groupInfo == null)
            {
                throw new ArgumentException("No information provided on add group");
            }
            lock (_Lock)
            {
                if (_Groups.ContainsKey(groupInfo.Groupname))
                {
                    throw new ArgumentException("Group " + groupInfo.Groupname + " is already created");
                }
                _Groups.Add(groupInfo.Groupname, new GroupInfo()
                {
                    Group = groupInfo
                });
                WriteGroups();
            }
        }

        public void UpdateGroup(Group groupInfo)
        {
            if (groupInfo == null)
            {
                throw new ArgumentException("No information provided on update group");
            }
            lock (_Lock)
            {
                if (!_Groups.ContainsKey(groupInfo.Groupname))
                {
                    throw new ArgumentException("Group " + groupInfo.Groupname + " does not exist on update group");
                }
                _Groups[groupInfo.Groupname].Group = groupInfo;
                WriteGroups();
            }
        }

        public void DeleteGroup(string groupname)
        {
            if (groupname == null)
            {
                throw new ArgumentException("No information provided on delete group");
            }
            lock (_Lock)
            {
                if (!_Groups.ContainsKey(groupname))
                {
                    throw new ArgumentException("Group " + groupname + " does not exist on delete group");
                }
                _Groups.Remove(groupname);
                WriteGroups();
            }
        }

        public void AddToGroup(string username, string groupname)
        {
            if (username == null || groupname == null)
            {
                throw new ArgumentException("No information provided on add to group");
            }
            lock (_Lock)
            {
                if (!_Groups.ContainsKey(groupname))
                {
                    throw new ArgumentException("Group " + groupname + " does not exist on add to group");
                }
                if (!_Groups[groupname].Group.Usernames.Contains(username))
                {
                    _Groups[groupname].Group.Usernames.Add(username);
                    WriteGroups();
                }
                if (!_Users.ContainsKey(username))
                {
                    throw new ArgumentException("User " + username + " does not exist on add to group");
                }
                if (!_Users[username].Groupnames.Contains(groupname))
                {
                    _Users[username].Groupnames.Add(groupname);
                    WriteUsers();
                }
            }
        }

        public void RemoveFromGroup(string username, string groupname)
        {
            if (username == null || groupname == null)
            {
                throw new ArgumentException("No information provided on remove from group");
            }
            lock (_Lock)
            {
                if (!_Groups.ContainsKey(groupname))
                {
                    throw new ArgumentException("Group " + groupname + " does not exist on remove from group");
                }
                if (_Groups[groupname].Group.Usernames.Contains(username))
                {
                    _Groups[groupname].Group.Usernames.Remove(username);
                    WriteGroups();
                }
                if (!_Users.ContainsKey(username))
                {
                    throw new ArgumentException("User " + username + " does not exist on remove from group");
                }
                if (_Users[username].Groupnames.Contains(groupname))
                {
                    _Users[username].Groupnames.Remove(groupname);
                    WriteUsers();
                }
            }
        }


        public bool Ping()
        {
            return true;
        }

        public bool Check()
        {
            try
            {
                ReadUsers();
                ReadGroups();
                return true;
            }
            catch (Exception ex)
            {
                Context.Log(SeverityType.Error, 0, "Failed to do check " + ex.Message);
                return false;
            }
        }

        private string ComputeHash(string password)
        {
            return Encoding.UTF8.GetString(System.Security.Cryptography.SHA512Managed.Create().ComputeHash(Encoding.UTF8.GetBytes(password)));
        }

        private void WriteUsers()
        {
            StringBuilder text = new StringBuilder();
            foreach (UserInfo userInfo in _Users.Values)
            {
                User user = userInfo.User;
                string password = userInfo.PasswordHash;
                List<string> groups = userInfo.Groupnames;

                //username:password:email:upn:givenname:surname:group,group,group
                text.AppendLine(user.Username + ":" + password + ":" + user.Email + ":" + user.UserPrincipalName + ":" + user.GivenName + ":" + user.Surname + ":" + string.Join(",", groups));
            }
            File.WriteAllText(_UsersFilename, text.ToString());
        }

        private void WriteGroups()
        {
            StringBuilder text = new StringBuilder();
            foreach (GroupInfo groupInfo in _Groups.Values)
            {
                Group group = groupInfo.Group;
                List<string> parents = groupInfo.ParentGroupnames;

                //group:parentgroup,parentgroup,parentgroup
                text.AppendLine(group.Groupname + ":" + string.Join(",", parents));
            }
            File.WriteAllText(_GroupsFilename, text.ToString());
        }

        private Dictionary<string, UserInfo> ReadUsers()
        {
            Dictionary<string, UserInfo> users = new Dictionary<string, UserInfo>();
            foreach (string line in File.ReadLines(_UsersFilename))
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    //username:password:email:upn:givenname:surname:group,group,group
                    string[] cols = line.Split(':');
                    string[] groups = cols[6].Split(',');
                    users.Add(cols[0], new UserInfo()
                    {
                        PasswordHash = cols[1],
                        User = new User()
                        {
                            Username = cols[0],
                            Email = cols[2],
                            UserPrincipalName = cols[3],
                            GivenName = cols[4],
                            Surname = cols[5],
                            Domain = null,
                            IsAdmin = false,
                            IsPlayer = false
                        },
                        Groupnames = new List<string>(groups)
                    });
                }
            }

            return users;
        }

        private Dictionary<string, GroupInfo> ReadGroups()
        {
            Dictionary<string, GroupInfo> groups = new Dictionary<string, GroupInfo>();
            foreach (string line in File.ReadLines(_GroupsFilename))
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    //group:parentgroup,parentgroup,parentgroup
                    string[] cols = line.Split(':');
                    string[] parentGroups = cols.Length > 1 ? cols[1].Split(',') : new string[0];
                    groups.Add(cols[0], new GroupInfo()
                    {
                        Group = new Group()
                        {
                            Groupname = cols[0],
                            Domain = null,
                            IsAdmin = false,
                            Usernames = new List<string>()
                        },
                        ParentGroupnames = new List<string>(parentGroups)
                    });
                }
            }
            return groups;
        }

        private Dictionary<string, GroupTree> OrganizeGroups()
        {
            //Organize the groups in an object tree structure
            Dictionary<string, GroupTree> groupTrees = new Dictionary<string, GroupTree>();
            List<string> subGroups = new List<string>();
            
            //First create all GroupTree nodes
            foreach (GroupInfo group in _Groups.Values)
            {
                if (!groupTrees.ContainsKey(group.Group.Groupname))
                {
                    groupTrees.Add(group.Group.Groupname, new GroupTree()
                        {
                            Groupname = group.Group.Groupname,
                            SubGroups = new List<GroupTree>()
                        });
                }
                else
                {
                    groupTrees[group.Group.Groupname].Groupname = group.Group.Groupname;
                }
            }

            //Then connect the sub group references
            foreach (GroupInfo group in _Groups.Values)
            {
                foreach (string g in group.ParentGroupnames)
                {
                    if (!groupTrees.ContainsKey(g))
                    {
                        groupTrees.Add(g, new GroupTree()
                            {
                                Groupname = g,
                                SubGroups = new List<GroupTree>()
                            });
                    }

                    groupTrees[g].SubGroups.Add(groupTrees[group.Group.Groupname]);
                }
            }

            return groupTrees;
        }

        private class GroupTree
        {
            public string Groupname;
            public List<GroupTree> SubGroups = new List<GroupTree>();
        }

        private class GroupInfo
        {
            public Group Group;
            public List<string> ParentGroupnames = new List<string>();
        }

        private class UserInfo
        {
            public User User;
            public string PasswordHash;
            public List<string> Groupnames = new List<string>();
        }
    }
}
