<?php
defined('_JEXEC') or die('Restricted access');
JHtml::_('behavior.tooltip');
JHtml::_('behavior.formvalidation');
  $searchid = JRequest::getVar('searchid', 0) ;
  $item = $this->sequencingbatch;
  $db =& JFactory::getDBO();
  $projectId = JRequest::getVar('ProjectId', -1);
  $batchNo = "";
  if ($searchid > 0)
    $batchNo = $item->sr_title;
  $where = "";
  if ($projectId != -1) {
    $where = " WHERE p.id = " . $projectId;
    $batchNo = "#" . JRequest::getVar('BatchNo', '1');
  } else {
    $where = ' WHERE p.status != "cancelled"';
  }
  $query = " SELECT p.id AS id, p.title, p.plateid, principalinvestigator," .
           " COUNT(b.id) AS batchcount, MAX(b.title) AS lastbatchtitle FROM #__aaaproject p" .
           " LEFT JOIN #__aaaclient c ON p.#__aaaclientid = c.id" .
           " LEFT JOIN #__aaasequencingbatch b ON b.#__aaaprojectid = p.id" .
           $where . " GROUP BY p.id ORDER by p.id DESC"; 
  $db->setQuery($query);
  $projects = $db->loadObjectList();
  $defaultBatchNos = array($batchNo);
  if ($projectId == -1) {
    foreach ($projects as $proj) {
      if (intval($proj->batchcount) > 0 && preg_match('/[0-9]+/', $proj->lastbatchtitle, $matches))
        $nextBatchNo = preg_replace('/[0-9]+/', intval($matches[0])+1, $proj->lastbatchtitle, 1);
      else
        $nextBatchNo = "#" . strval(intval($proj->batchcount) + 1);
      $defaultBatchNos[] = $nextBatchNo;
    }
  }
  $query = " SELECT id, primername FROM #__aaasequencingprimer ";
  $db->setQuery($query);
  $primers = $db->loadObjectList();
?>

<script type="text/javascript">
function setBatchNo()
{
  <?php
    $nos = implode('","', $defaultBatchNos);
    echo " var batchTitles = new Array(\"$nos\");"
  ?>
  var idx = document.getElementById("projectSelect").selectedIndex;
  document.getElementById("title").value = batchTitles[idx];
  return true;
}
function validatePrimers()
{
  if (document.getElementById("#__aaasequencingprimerid").value == "0") {
    alert("You must select a sequencing primer or 'None'!");
    return false;
  }
  if (document.getElementById("indexprimerid").value == "0") {
    alert("You must select an index primer or 'None'!");
    return false;
  }
  return true;
}
</script>
<form  enctype="multipart/form-data" action="<?php echo JText::_('?option=com_dbapp&view=sequencingbatch&layout=save&id='.(int) $searchid); ?>" method="post" name="adminForm" id="admin-form" class="form-validate">
      <?php
      if ($searchid > 0) {
        echo "<h1> Edit sequencing batch </h1>";
      } else {
        echo "<h1> Add new sequencing batch </h1>";
      }
      ?>
 <div class='sequencingbatch'>
  <fieldset>
    <legend></legend>
    <table>
      <tr>
        <th>Sample&nbsp;</th>
        <td>
  <?php
  if ($projectId != -1) {
      $pi = $projects[0];
      echo $pi->plateid . JText::_(' - ') . "P.I: "
           . (($pi->principalinvestigator != "")? $pi->principalinvestigator : "Unknown");
      echo '<input type="hidden" name="#__aaaprojectid" id="#__aaaprojectid" value="' . $projectId . '" />';
  } else { ?>
          <select name="#__aaaprojectid" id="projectSelect" onchange="setBatchNo();">
            <option value="0">Choose a Project</option>
            <?php foreach ($projects as $pi) : ?>
            <option value="<?php echo $pi->id; ?>"
              <?php if ($searchid > 0)  { if ($pi->id == $item->aaaprojectid) echo ' selected="selected" '; } ?> >
              <?php echo $pi->plateid . JText::_(' - ') . "P.I: "
                         . (($pi->principalinvestigator != "")? $pi->principalinvestigator : "Unknown"); ?>
            </option>
          <?php endforeach; ?>
          </select>
        <?php } ?>
        </td>
      </tr>

      <tr>
        <th>Batch&nbsp;no&nbsp;</th>
        <td><input type="text" name="title" id="title" value="<?php echo $batchNo; ?>" class="inputbox" size="40" /></td>
      </tr>

<!--
      <tr><th>Lab&nbsp;book page</th>
	      <td><input type="text" name="labbookpage" id="labbookpage" value="<?php if ($searchid > 0) echo $item->labbookpage; ?>" class="inputbox" size="40"/></td>
	  </tr>
-->
      <tr>
        <th>Planned&nbsp;number of cycles&nbsp;</th>
        <td><input type="text" name="plannednumberofcycles" id="plannednumberofcycles" value="<?php if ($searchid > 0) echo $item->plannednumberofcycles; ?>" class="inputbox" size="40"/></td>
      </tr>

      <tr>
        <th>Sequencing Primer&nbsp;</th>
        <td><select name="#__aaasequencingprimerid" id="#__aaasequencingprimerid" >
              <option value="0">Choose a Primer</option>
                <?php foreach ($primers as $pi) : ?>
              <option value="<?php echo $pi->id; ?>"
                <?php if ($searchid > 0)  { if ($pi->id == $item->primerid) echo ' selected = "selected" '; } ?> >
                <?php echo $pi->primername; ?>
              </option>
                <?php endforeach; ?>
            </select>
        </td>
      </tr>

      <tr>
        <th>Number of index cycles&nbsp;</th>
        <td><input type="text" name="plannedindexcycles" id="plannedindexcycles" value="<?php if ($searchid > 0) echo $item->plannedindexcycles; ?>" class="inputbox" size="40"/></td>
      </tr>

      <tr>
       <th>Index primer&nbsp;</th>
       <td><select name="indexprimerid" id="indexprimerid" >
             <option value="0">Choose an index Primer</option>
              <?php foreach ($primers as $pi) : ?>
             <option value="<?php echo $pi->id; ?>"
                <?php if ($searchid > 0)  { if ($pi->id == $item->indexprimerid) echo ' selected = "selected" '; } ?> >
                <?php echo $pi->primername; ?>
             </option>
              <?php endforeach; ?>
          </select></td>
      </tr>

      <tr>
        <th>Planned&nbsp;number of lanes&nbsp;</th>
        <td><input type="text" name="plannednumberoflanes" id="plannednumberoflanes" value="<?php if ($searchid > 0) echo $item->plannednumberoflanes; ?>" class="inputbox required" size="40"/></td>
      </tr>
      <tr>
        <th>Cost&nbsp;</th>
	<td><input type="text" name="cost" id="cost" value="<?php if ($searchid > 0) echo $item->cost; ?>" class="inputbox" size="40"/></td>
      </tr>

      <tr>
        <th>Invoice sent?&nbsp;</th>
          <td>
            <select name="invoice" id="invoice" >
              <option value="unknown" <?php if ($searchid > 0)  { if ('unknown' == $item->invoice) echo ' selected="selected" '; } ?> ><?php echo JText::_('unknown'); ?></option>
              <option value="sent" <?php if ($searchid > 0)  { if ('sent' == $item->invoice) echo ' selected="selected" '; } ?> ><?php echo JText::_('sent'); ?></option>
              <option value="not sent" <?php if ($searchid > 0)  { if ('not sent' == $item->invoice) echo ' selected="selected" '; } ?> ><?php echo JText::_('not sent'); ?></option>
            </select></td>
      </tr>

      <tr>
       <th>Costs signed?&nbsp;</th>
        <td><select name="signed" id="signed" >
              <option value="unknown" <?php if ($searchid > 0) { if ('unknown' == $item->signed) echo ' selected="selected" '; } ?> ><?php echo JText::_('unknown'); ?></option>
              <option value="yes" <?php if ($searchid > 0)  { if ('yes' == $item->signed) echo ' selected="selected" '; } ?> ><?php echo JText::_('yes'); ?></option>
              <option value="no" <?php if ($searchid > 0)  { if ('no' == $item->signed) echo ' selected="selected" '; } ?> ><?php echo JText::_('no'); ?></option>
            </select></td>
      </tr>

      <tr>
        <th>Comment&nbsp;</th>
        <td><input type="text" name="comment" id="comment" value="<?php if ($searchid > 0) echo $item->comment; ?>" class="inputbox" size="40"/></td>
      </tr>

<?php
    $user =& JFactory::getUser();
    date_default_timezone_set('Europe/Stockholm');
    $today = date("Y-m-d H:i:s");
 if ($searchid > 0) {
    echo "<tr></tr><tr><td>User: " . $item->user . "</td><td>";
    echo "Creation&nbsp;date: " . $item->time . "</td></tr>";
    echo "<tr><td colspan='2'>will be replaced by</td></tr>";
    echo "<tr><td>User: " . $user->username . "</td><td>";
    echo "Creation&nbsp;date: " . $today . "</td></tr>";
 } else {
    echo "<tr><td>User: " . $user->username . "</td><td>";
    echo "Creation&nbsp;date: " . $today . "</td></tr>";

 }
?>
    </table>
  </fieldset>
</div>
<br/>
<input type="Submit" name="Submit" value="Save" onclick="return validatePrimers();">
<input type="Submit" name="Submit" value="Cancel" >
<?php
    $menus = &JSite::getMenu();
    $menu  = $menus->getActive();
    $itemid = $menu->id;
    echo "<a href=index.php?option=com_dbapp&view=sequencingbatches&Itemid=" . $itemid . ">Return to list of sequencing batches</a><br/>&nbsp;<br/>";

    echo '<input type="hidden" name="user" value="' . $user->username . '" />';
    echo '<input type="hidden" name="time" value="' . $today . '" />';
    if ($searchid == 0) {
    } else {
      echo '<input type="hidden" name="id" value="' . $item->id . '" />';
    }


?>
		<input type="hidden" name="task" value="save" />
		<?php echo JHtml::_('form.token'); ?>
</form>

