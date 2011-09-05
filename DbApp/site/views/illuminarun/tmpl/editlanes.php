<?php
defined('_JEXEC') or die('Restricted access');
JHtml::_('behavior.tooltip');
JHtml::_('behavior.formvalidation');
  $searchid = JRequest::getVar('searchid') ;

  foreach ($this->illuminaruns as $lane) {
    $irilluminarunid = $lane->illuminarunid;
    $irdbid = $lane->id;
    $irrundate = $lane->rundate;
    $ircomment = $lane->comment;
    $iruser = $lane->user;
    $irtime = $lane->time;
  }

  $db =& JFactory::getDBO();
  $qhaving = (searchid > 0)? "": " HAVING plannednumberoflanes - COUNT(l.id) > 0 ";
  $query = " SELECT b.title AS batchtitle, b.id AS id, p.title as title, plateid,  plannednumberoflanes,
               plannednumberoflanes - COUNT(l.id) AS unassignedlanes
               FROM jos_aaasequencingbatch b
             LEFT JOIN jos_aaaproject p ON p.id=b.jos_aaaprojectid
             LEFT JOIN jos_aaalane l ON l.jos_aaasequencingbatchid = b.id
             WHERE p.status != 'cancelled' AND b.id > 0 GROUP BY b.id $having ORDER BY plateid DESC ";
  $db->setQuery($query);
  $seqruns = $db->loadObjectList();
?>

<script type="text/javascript">
function validateBatchIds()
{
  for ($i = 1; $i <= 8; $i++) {
    if (document.getElementById("#__aaasequencingbatchid" + $i).value == "X") {
      alert("You must select a batch for lane " + $i);
      return false;
    }
  }
  return true;
}
</script>
<form action="<?php echo JText::_('?option=com_dbapp&view=illuminarun&layout=savelanes&id='.(int) $searchid); ?>" method="post" name="adminForm" id="admin-form" class="form-validate">
<?php
  if ($searchid > 0) {
    echo "<h1> Edit lanes of Illumina run $irilluminarunid </h1>";
  } else {
    echo "<h1> You have to define the Illumina run before you add lanes </h1>";
  }
    $user =& JFactory::getUser();
    date_default_timezone_set('Europe/Stockholm');
    $today = date("Y-m-d H:i:s");

?>
<div class='illuminarun'>
<fieldset>
  <legend>Illumina run&nbsp;
    <?php if ($searchid > 0) echo $irilluminarunid; else JText::_('Please, define Illumina run first')?>
  </legend>
  <table>
    <tr><th>Run&nbsp;date&nbsp;</th><td><?php if ($searchid > 0) echo $irrundate; ?></td></tr>
    <tr><th>Comment&nbsp;</th><td><?php if ($searchid > 0) echo $ircomment; ?></td></tr>
    <tr><th>User&nbsp;</th><td><?php if ($searchid > 0) echo $iruser; ?></td></tr>
    <tr><th>Time&nbsp;</th><td><?php if ($searchid > 0) echo $irtime; ?></td></tr>
  </table>
</fieldset>
</div>
<div class='lanes'>
<fieldset>   <legend>Edit Lanes for this Illumina run</legend>
  <table>
    <tr> 
      <th>Lane&nbsp;no</th>
      <th>Cycles</th>
      <th>Seq.&nbsp;batch</th>
      <th>Conc&nbsp;[pM]&nbsp;</th>
      <th>Yield&nbsp;</th>
      <th>Comment&nbsp;</th>
      <th>User&nbsp;</th>
      <th>Last change&nbsp;</th>
    </tr>
<?php
  foreach ($this->illuminaruns as $lane) {
?>
      <td><center><b><?php if ($searchid > 0) echo $lane->laneno; ?></b></center></td>
      <td><input type="text" name="cycles<?php if ($searchid > 0) echo $lane->laneno; ?>" id="cycles<?php if ($searchid > 0) echo $lane->laneno; ?>" value="<?php if ($searchid > 0) echo $lane->cycles; ?>" class="inputbox" size="8"/></td>
      <td>
        <select name="#__aaasequencingbatchid<?php if ($searchid > 0) echo $lane->laneno; ?>" id="#__aaasequencingbatchid<?php if ($searchid > 0) echo $lane->laneno; ?>" >
          <option value="X"<?php if ($searchid > 0 && $lane->Sid === null) echo 'selected="selected"'; ?>>Choose batch</option>
          <option value="0" <?php if ($searchid > 0 && $lane->Sid === "0") echo 'selected="selected"'; ?>>EMPTY</option>
<?php foreach ($seqruns as $seqrun) : ?>
          <option value="<?php echo $seqrun->id . '"';
                           $nunass = ($seqrun->unassignedlanes == null)? "?" : $seqrun->unassignedlanes;
                           if ($searchid > 0 && $seqrun->id == $lane->Sid) echo ' selected="selected" ';
                           echo "> $seqrun->plateid - $seqrun->batchtitle ($nunass unassigned lanes)" ?></option>
<?php endforeach; ?>
        </select>
      </td>
      <td><input type="text" name="molarconcentration<?php if ($searchid > 0) echo $lane->laneno; ?>" id="molarconcentration<?php if ($searchid > 0) echo $lane->laneno; ?>" value="<?php if ($searchid > 0) echo $lane->molarconcentration; ?>" class="inputbox" size="8"/></td>
      <td><input type="text" name="yield<?php if ($searchid > 0) echo $lane->laneno; ?>" id="yield<?php if ($searchid > 0) echo $lane->laneno; ?>" value="<?php if ($searchid > 0) echo $lane->yield; ?>" class="inputbox" size="6"/></td>
      <td><input type="text" name="comment<?php if ($searchid > 0) echo $lane->laneno; ?>" id="comment<?php if ($searchid > 0) echo $lane->laneno; ?>" value="<?php if ($searchid > 0) echo $lane->Lcomment; ?>" class="inputbox" size="20"/></td>
      <td> &nbsp;<?php if ($searchid > 0) echo $lane->Luser; ?></td>
      <td> &nbsp;<?php if ($searchid > 0) echo $lane->Ltime; ?></td>
    </tr>

    <input type="hidden" name="id<?php if ($searchid > 0) echo $lane->laneno; ?>" value="<?php if ($searchid > 0) echo $lane->Lid; ?>">
    <input type="hidden" name="user<?php if ($searchid > 0) echo $lane->laneno; ?>" value="<?php if ($searchid > 0) echo $user->username; ?>" />
    <input type="hidden" name="time<?php if ($searchid > 0) echo $lane->laneno; ?>" value="<?php if ($searchid > 0) echo $today; ?>" />

<?php
   }
?>
  </table>
</fieldset>
</div>
<br/>
<input type="Submit" name="Submit" value="Save">
<input type="Submit" name="Submit" value="Cancel" >
<?php
    $menus = &JSite::getMenu();
    $menu  = $menus->getActive();
    $itemid = $menu->id;
    echo "<a href=index.php?option=com_dbapp&view=illuminaruns&Itemid=" . $itemid . ">Return to list of Illumina runs</a><br/>&nbsp;<br/>";

    if ($searchid == 0) {
    } else {
      echo '<input type="hidden" name="id" value="' . $searchid . '" />';
    }


?>
		<input type="hidden" name="task" value="savelanes" />
		<?php echo JHtml::_('form.token'); ?>
</form>

