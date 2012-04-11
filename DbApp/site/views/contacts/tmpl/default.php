<?php
defined('_JEXEC') or die('Restricted access');
?>
<?php 
  echo "<h1>List of contacts</h1>";
  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
  $itemid = $menu->id;
  $newlink = "<a href=index.php?option=com_dbapp&view=contact&layout=edit&controller=contact&searchid=0&Itemid=" . $itemid . " >&nbsp;Add&nbsp;new&nbsp;contact&nbsp;</a>";
  echo "<div class='contact'><fieldset><legend>$newlink</legend>\n";
  echo "<table>\n";
  echo "<tr><th>&nbsp;Contact&nbsp;person&nbsp;</th>
            <th>&nbsp;Email&nbsp;</th>
            <th>&nbsp;Phone&nbsp;no&nbsp;</th>
            <th>&nbsp;Samples&nbsp;</th>
            <th>&nbsp;Latest&nbsp;edit&nbsp;</th></tr>\n";
  echo "<tr><td>&nbsp;(click=view)</td><td>&nbsp;(click=edit)</td><td></td></tr>\n";
  foreach ($this->contacts as $contact) {
    if ($contact->contactperson == "?") continue;
    echo "<tr>";
    $contactlink = "<a href=index.php?option=com_dbapp&view=contact&layout=contact&controller=contact&searchid=" 
           . $contact->id . "&Itemid=" . $itemid . ">";
    $editlink = "<a href=index.php?option=com_dbapp&view=contact&layout=edit&controller=contact&searchid=" 
           . $contact->id . "&Itemid=" . $itemid . ">";
    echo "<td>&nbsp;" . $contactlink . $contact->contactperson . "</a></td>";
    echo "<td>&nbsp;" . $editlink . $contact->contactemail . "</a></td>";
    echo "<td>&nbsp;" . $contact->contactphone . "</td>";
    $projlink = "<a href=index.php?option=com_dbapp&view=projects&layout=default&contactId=" . urlencode( $contact->contactperson ) . ">" . $contact->projectcount . "</a>";
    echo "<td>&nbsp;" . $projlink . "</td>";
    echo "<td>&nbsp; $contact->user &nbsp;&nbsp; $contact->time </td>";
    echo "</tr>\n";
  }
  echo "</table></fieldset></div>\n";
?>

