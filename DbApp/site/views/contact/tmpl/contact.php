<?php // no direct access
defined('_JEXEC') or die('Restricted access'); ?>
<?php 
  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
  $itemid = $menu->id;
  $searchid = JRequest::getVar('searchid') ;
  $contact = $this->contact;
  $editlink = "<a href=index.php?option=com_dbapp&view=contact&layout=edit&controller=contact&searchid=" 
           . $contact->id . "&Itemid=" . $itemid . ">Edit this contact</a>";
  echo "<h1>View of contact " . $contact->contactperson . "</h1>\n";
  echo "<div class='contact'><fieldset><legend>$editlink</legend>\n";
  echo "  <p> Contacts are appointed by the client (P.I.) </p>\n";
  echo "  <table>\n";
  echo "    <tr><th>Email:&nbsp;</th><td>" . $contact->contactemail . "</td></tr>\n";
  echo "    <tr><th>Phone&nbsp;no:&nbsp;</th><td>" . $contact->contactphone . "</td></tr>\n";
  echo "    <tr><th>Comment:&nbsp;</th><td>" . $contact->comment . "</td></tr>\n";
  echo "    <tr><th>Latest&nbsp;edit:&nbsp;</th><td>" . $contact->user . "&nbsp;&nbsp;" . $contact->time . "</td></tr>\n";
  echo "  </table></fieldset></div><br />\n";
  echo "<br />\n";
  echo  "<a href=index.php?option=com_dbapp&view=projects&layout=default&contactId=" . urlencode( $contact->contactperson ) . ">View contact's samples</a><br />\n";
  echo "<br />\n";
  echo "<a href=index.php?option=com_dbapp&view=contacts&Itemid=" . $itemid . ">Return to contact list</a>\n";
?>

