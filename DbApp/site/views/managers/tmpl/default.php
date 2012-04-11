<?php
defined('_JEXEC') or die('Restricted access');
?>
<?php 
  echo "<h1>List of managers</h1>";
  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
  $itemid = $menu->id;
  $newlink = "<a href=index.php?option=com_dbapp&view=manager&layout=edit&controller=manager&searchid=0&Itemid=" . $itemid . " >&nbsp;Add&nbsp;new&nbsp;manager&nbsp;</a>";
  echo "<div class='manager'><fieldset><legend>$newlink</legend>\n";
  echo "<table>\n";
  echo "<tr><th>&nbsp;Manager&nbsp;</th>
            <th>&nbsp;Email&nbsp;</th>
            <th>&nbsp;Phone&nbsp;no&nbsp;</th>
            <th>&nbsp;Samples&nbsp;</th>
            <th>&nbsp;Latest&nbsp;edit&nbsp;</th></tr>\n";
  echo "<tr><td>&nbsp;(click=view)</td><td>&nbsp;(click=edit)</td><td></td></tr>\n";
  foreach ($this->managers as $manager) {
    if ($manager->person == "?") continue;
    echo "<tr>";
    $managerlink = "<a href=index.php?option=com_dbapp&view=manager&layout=manager&controller=manager&searchid=" 
           . $manager->id . "&Itemid=" . $itemid . ">";
    $editlink = "<a href=index.php?option=com_dbapp&view=manager&layout=edit&controller=manager&searchid=" 
           . $manager->id . "&Itemid=" . $itemid . ">";
    echo "<td>&nbsp;" . $managerlink . $manager->person . "</a></td>\n";
    echo "<td>&nbsp;" . $editlink . $manager->email . "</a></td>\n";
    echo "<td>&nbsp;" . $manager->phone . "</td>\n";
    $projlink = "<a href=index.php?option=com_dbapp&view=projects&layout=default&managerId=" . urlencode( $manager->person ) . ">" . $manager->projectcount . "</a>";
    echo "<td>&nbsp;" . $projlink . "</td>\n";
    echo "<td><nobr>&nbsp; $manager->user &nbsp;&nbsp $manager->time </nobr></td>\n";
    echo "</tr>\n";
  }
  echo "</table></fieldset></div>\n";
?>

