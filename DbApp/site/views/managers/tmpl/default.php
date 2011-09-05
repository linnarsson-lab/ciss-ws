<?php
defined('_JEXEC') or die('Restricted access');
?>
<?php 
  echo "<H1>Managers - Summary list VIEW</H1>";
  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
  $itemid = $menu->id;
  $newlink = "<a href=index.php?option=com_dbapp&view=manager&layout=edit&controller=manager&searchid=0&Itemid=" . $itemid . " >&nbsp;Add&nbsp;new&nbsp;manager&nbsp;</a>";
  echo "<div class='manager'><fieldset><legend>List of managers</legend><table>";
  echo "<tr><th>View</th><th>Edit</th><th>$newlink</th><th></th></tr>";
  echo "<tr><th>&nbsp;Manager&nbsp;</th>
            <th>&nbsp;Email&nbsp;</th>
            <th>&nbsp;Phone&nbsp;no&nbsp;</th>
            <th>&nbsp;Samples&nbsp;</th>
            <th>&nbsp;Latest&nbsp;edit&nbsp;</th></tr>";
  foreach ($this->managers as $manager) {
    echo "<tr>";
    $managerlink = "<a href=index.php?option=com_dbapp&view=manager&layout=manager&controller=manager&searchid=" 
           . $manager->id . "&Itemid=" . $itemid . ">";
    $editlink = "<a href=index.php?option=com_dbapp&view=manager&layout=edit&controller=manager&searchid=" 
           . $manager->id . "&Itemid=" . $itemid . ">";
    echo "<td>&nbsp;" . $managerlink . $manager->person . "</a></td>";
    echo "<td>&nbsp;" . $editlink . $manager->email . "</a></td>";
    echo "<td>&nbsp;" . $manager->phone . "</td>";
    $projlink = "<a href=index.php?option=com_dbapp&view=projects&layout=default&managerId=" . urlencode( $manager->person ) . ">" . $manager->projectcount . "</a>";
    echo "<td>&nbsp;" . $projlink . "</td>";

//    echo "<td>" . $manager->comment . "</td>";
    echo "<td><nobr>&nbsp;" . $manager->user . "&nbsp; &nbsp;" ;
    echo $manager->time . " </nobr></td>";
    echo "</tr>";
  }
  echo "</table></fieldset></div><br />&nbsp;<br />";

?>

