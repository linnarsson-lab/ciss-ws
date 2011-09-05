<?php

// no direct access
defined('_JEXEC') or die('Restricted access');
jimport('joomla.html.toolbar');
 
class DbAppHelperToolbar extends JObject {        

  function getToolbar() {
    $bar =& new JToolBar( 'DbApp Toolbar' );
    $bar->appendButton( 'Standard', 'new', 'New Record', 'new', false );
    $bar->appendButton( 'Standard', 'edit', 'Edit Record', 'edit', false );
//    $bar->appendButton( 'Separator' );
    $bar->appendButton( 'Standard', 'delete', 'Delete Record', 'delete', false );
    return $bar->render();
  }
 
}
