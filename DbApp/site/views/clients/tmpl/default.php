<?php
defined('_JEXEC') or die('Restricted access');
?>
<?php 
  echo "<H1>Clients - Summary list VIEW</H1>";
  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
  $itemid = $menu->id;
  $newlink = "<a href=index.php?option=com_dbapp&view=client&layout=edit&controller=client&searchid=0&Itemid=" . $itemid . " >&nbsp;Add&nbsp;new&nbsp;client&nbsp;</a>";
  echo "<div class='client'><fieldset><legend>List of clients</legend><table>";
  echo "<tr><th>View</th><th>Edit</th><th>$newlink</th><th></th><th></th></tr>";
  echo "<tr><th>&nbsp;Principal&nbsp;Investigator&nbsp;</th>
            <th>&nbsp;Department&nbsp;</th>
            <th>&nbsp;Samples&nbsp;</th>
            <th>&nbsp;Category&nbsp;</th>
            <th>&nbsp;Latest&nbsp;edit&nbsp;</th></tr>";
  foreach ($this->clients as $client) {
    echo "<tr>";
    $clientlink = "<a href=index.php?option=com_dbapp&view=client&layout=client&controller=client&searchid=" 
           . $client->id . "&Itemid=" . $itemid . ">";
    $editlink = "<a href=index.php?option=com_dbapp&view=client&layout=edit&controller=client&searchid=" 
           . $client->id . "&Itemid=" . $itemid . ">";
    echo "<td>&nbsp;" . $clientlink . $client->principalinvestigator . "</a></td>";
    echo "<td>&nbsp;" . $editlink . $client->department . "</a></td>";
    $projlink = "<a href=index.php?option=com_dbapp&view=projects&layout=default&clientId=" . urlencode( $client->principalinvestigator ) . ">" . $client->projectcount . "</a>";
    echo "<td>&nbsp;" . $projlink . "</td>";
    //echo "<td>&nbsp;" . $client->projectcount . "</td>";
    echo "<td>&nbsp;" . $client->category . "</td>";
    echo "<td>&nbsp;" . $client->user . " &nbsp; &nbsp; &nbsp; " . $client->time . "</td>";
    echo "<tr>";
  }
  echo "</table></fieldset></div><br />";

?>

<!--<h1>?php echo $this->item->principalinvestigator.(($this->item->category and $this->item->params->get('show_category')) ? (' ('.$this->item->category.')') : ''); ?></h1>
?php
-->
