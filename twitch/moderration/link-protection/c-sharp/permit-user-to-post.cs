using System;

// The following code was not originally written by me - proper credit goes to emongev (https://www.twitch.tv/emongev)
public class CPHInline
{
    public bool Execute()
    {
        // How long (in seconds) to allow a user that has been permitted to post a link in chat
        int TIME_TO_PERMIT_SECONDS = Convert.ToInt32(args["permitTime"].ToString());

        // Name of user that you have allowed to post
        string userToGivePermitTo = args["targetUser"].ToString();
        string currentPermitUser = CPH.GetGlobalVar<string>("permitUser", false);

        // If we do not currently have an open window for another user to post, set the requesting user's permission
        if (currentPermitUser == null || currentPermitUser == "")
        {
            CPH.SetGlobalVar("permitUser", userToGivePermitTo, false);
            CPH.SendMessage("/me " + userToGivePermitTo + " you have permission to post links for " + TIME_TO_PERMIT_SECONDS.ToString() + " seconds.");
        }
        else
        {
            CPH.SendMessage("/me Someone else is being permitted to post links currently. Please try again in a bit.");
        }

        // Tell our bot to wait for the allotted time before removing permissions
        CPH.Wait(1000 * TIME_TO_PERMIT_SECONDS);

        // Permitted amount of time has passed, remove current user permission to post
        CPH.SetGlobalVar("permitUser", "", false);

        return true;
    }
}