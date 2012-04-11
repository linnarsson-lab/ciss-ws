<?php
defined('_JEXEC') or die('Restricted access');
?>
<?php 
  echo "<h1>List of clients</h1>\n";
  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
  $itemid = $menu->id;
  $newlink = "<a href=index.php?option=com_dbapp&view=client&layout=edit&controller=client&searchid=0&Itemid=" . $itemid . " >&nbsp;Add&nbsp;new&nbsp;client&nbsp;</a>";
  echo "<div class='client'><fieldset><legend>$newlink</legend>\n";
  echo "<table>\n";
  echo "<tr><th>&nbsp;Principal&nbsp;Investigator&nbsp;</th>
            <th>&nbsp;Department&nbsp;</th>
            <th>&nbsp;Samples&nbsp;</th>
            <th>&nbsp;Category&nbsp;</th>
            <th>&nbsp;Latest&nbsp;edit&nbsp;</th></tr>";
  echo "<tr><td>&nbsp;(click=view)</td><td>&nbsp;(click=edit)</td><td></td><td></td></tr>\n";
  foreach ($this->clients as $client) {
    if ($client->principalinvestigator == "?") continue;
    echo "<tr>\n";
    $clientlink = "<a href=index.php?option=com_dbapp&view=client&layout=client&controller=client&searchid=" 
           . $client->id . "&Itemid=" . $itemid . ">";
    $editlink = "<a href=index.php?option=com_dbapp&view=client&layout=edit&controller=client&searchid=" 
           . $client->id . "&Itemid=" . $itemid . ">";
    echo "<td>&nbsp;" . $clientlink . $client->principalinvestigator . "</a></td>\n";
    echo "<td>&nbsp;" . $editlink . $client->department . "</a></td>\n";
    $projlink = "<a href=index.php?option=com_dbapp&view=projects&layout=default&clientId=" . urlencode( $client->principalinvestigator ) . ">" . $client->projectcount . "</a>";
    echo "<td>&nbsp;" . $projlink . "</td>\n";
    echo "<td>&nbsp;" . $client->category . "</td>\n";
    echo "<td>&nbsp; $client->user &nbsp;&nbsp; $client->time </td>\n";
    echo "<tr>\n";
  }
  echo "</table></fieldset></div>\n";
?>

