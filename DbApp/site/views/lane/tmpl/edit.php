<?php
defined('_JEXEC') or die('Restricted access');
JHtml::_('behavior.tooltip');
JHtml::_('behavior.formvalidation');
  $searchid = JRequest::getVar('searchid') ;
  $lane = $this->lane;
  if ($searchid > 0) {
    echo "<H1> Edit lane record &nbsp; &nbsp; &nbsp; &nbsp; id: " . $lane->id . " </H1><BR />";
  } else {
    echo "<H1> Add new lane info&nbsp; &nbsp; &nbsp; </H1><BR />";
  }

?>

<form action="<?php echo JText::_('?option=com_dbapp&view=lane&layout=save&id='.(int) $searchid); ?>" method="post" name="adminForm" id="admin-form" class="form-validate">
<?php
  $db =& JFactory::getDBO();
  $query = " SELECT id, illuminarunid, rundate FROM #__aaailluminarun ";
  $db->setQuery($query);
  $illruns = $db->loadObjectList();
  $query = " SELECT #__aaasequencingbatch.id AS id, title, plateid FROM #__aaasequencingbatch, #__aaaproject WHERE #__aaaproject.id = #__aaasequencingbatch.#__aaaprojectid ";
  $db->setQuery($query);
  $seqbatches = $db->loadObjectList();

?>
<table>
<tr><td>Illumina&nbsp;run&nbsp;</td><td>
<select name="#__aaailluminarunid" id="#__aaailluminarunid" ><option value="0">Choose Illumina run</option>
<?php foreach ($illruns as $illrun) : ?>
    <option value="<?php echo $illrun->id; ?>" <?php if ($searchid > 0)  { if ($illrun->id == $lane->aaailluminarunid) echo ' selected = "selected" '; } ?> ><?php echo $illrun->illuminarunid . JText::_(' - ') . $illrun->rundate; ?></option>
<?php endforeach; ?>
</select></td></tr>

<tr><td>Sequencing&nbsp;batch&nbsp;</td><td>
<select name="#__aaasequencingbatchid" id="#__aaasequencingbatchid" ><option value="0">Choose Sequencing run</option>
<?php foreach ($seqbatches as $seqbatch) : ?>
    <option value="<?php echo $seqbatch->id; ?>" <?php if ($searchid > 0)  { if ($seqbatch->id == $lane->aaasequencingbatchid) echo ' selected = "selected" '; } ?> ><?php echo $seqbatch->plateid . JText::_(' - ') . $seqbatch->title . JText::_(' [') .  $seqbatch->id . JText::_(']'); ?></option>
<?php endforeach; ?>
</select></td></tr>

<tr><td>Lane&nbsp;no&nbsp;</td><td>
<input type="text" name="laneno" id="laneno" value="<?php if ($searchid > 0) echo $lane->laneno; ?>" class="inputbox required" size="40"/></td></tr>
<tr><td>Cycle&nbsp;count&nbsp;</td><td>
<input type="text" name="cycles" id="cycles" value="<?php if ($searchid > 0) echo $lane->cycles; ?>" class="inputbox" size="40"/></td></tr>
<tr><td>Concentration&nbsp;[pM]&nbsp;</td><td>
<input type="text" name="molarconcentration" id="molarconcentration" value="<?php if ($searchid > 0) echo $lane->molarconcentration; ?>" class="inputbox required" size="40"/></td></tr>
<tr><td>Yield&nbsp;</td><td>
<input type="text" name="yield" id="yield" value="<?php if ($searchid > 0) echo $lane->yield; ?>" class="inputbox" size="40"/></td></tr>
<tr><td>Comment&nbsp;</td><td>
<input type="text" name="comment" id="comment" value="<?php if ($searchid > 0) echo $lane->comment; ?>" class="inputbox" size="40"/></td></tr>

<?php
    $user =& JFactory::getUser();
    date_default_timezone_set('Europe/Stockholm');
    $today = date("Y-m-d H:i:s");
 if ($searchid > 0) {
    echo "<tr><td>User: " . $lane->user . "</td><td>";
    echo "Creation&nbsp;date: " . $lane->time . "</td></tr>";
    echo "<tr><td colspan='2'>Will be replaced by</td></tr>";
    echo "<tr><td>User: " . $user->username . "</td><td>";
    echo "Creation&nbsp;date: " . $today . "</td></tr>";
 } else {
    echo "<tr><td>User: " . $user->username . "</td><td>";
    echo "Creation&nbsp;date: " . $today . "</td></tr>";

 }
?>
</table>
<br/>
<input type="Submit" name="Submit" value="Save">
<input type="Submit" name="Submit" value="Cancel" >
<?php
    $menus = &JSite::getMenu();
    $menu  = $menus->getActive();
    $itemid = $menu->id;
    echo "<a href=index.php?option=com_dbapp&view=lanes&Itemid=" . $itemid . ">Return to list</a>";

    echo '<input type="hidden" name="user" value="' . $user->username . '" />';
    echo '<input type="hidden" name="time" value="' . $today . '" />';
    if ($searchid == 0) {
    } else {
      echo '<input type="hidden" name="id" value="' . $lane->id . '" />';
    }


?>
		<input type="hidden" name="task" value="save" />
		<?php echo JHtml::_('form.token'); ?>
</form>






