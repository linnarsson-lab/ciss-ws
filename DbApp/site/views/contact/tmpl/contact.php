<?php // no direct access
defined('_JEXEC') or die('Restricted access'); ?>
<?php 
  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
  $itemid = $menu->id;
  $searchid = JRequest::getVar('searchid') ;
  $contact = $this->contact;
  $editlink = "<a href=index.php?option=com_dbapp&view=contact&layout=edit&controller=contact&searchid=" 
           . $contact->id . "&Itemid=" . $itemid . ">Edit this record</a>";
    echo "<H1>Contact - single record VIEW</H1>";
    echo "<div class='contact'><fieldset><legend> Contact &nbsp; '" . $contact->contactperson . "' &nbsp; &nbsp; &nbsp; &nbsp; DB id: '" . $contact->id . "' &nbsp; &nbsp; &nbsp; " . $editlink . "</legend>";
    echo "<p> Contacts are appointed by the client (PI) </p>";
    echo "<table><tr><th>Email:&nbsp;</th><td>" . $contact->contactemail . "</td></tr>";
    echo "<tr><th>Phone&nbsp;no:&nbsp;</th><td>" . $contact->contactphone . "</td></tr>";
    echo "<tr><th>Comment:&nbsp;</th><td>" . $contact->comment . "</td></tr>";
    echo "<tr><th>User:&nbsp;</th><td>" . $contact->user . "</td></tr>";
    echo "<tr><th>Latest&nbsp;edit:&nbsp;</th><td>" . $contact->time . "</td></tr></table></fieldset></div>";
    echo "<br />";
    echo "<a href=index.php?option=com_dbapp&view=contacts&Itemid=" . $itemid . ">Return to list</a>";
?>

